using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Core.AudioDirector.Targets;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Core.AudioDirector;

public sealed class AudioDirectorSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<int, AudioGroup> _groups = new();
    private readonly Dictionary<(int GroupId, int TrackId), TrackPlaybackState> _playbackStates = new();
    private readonly Dictionary<(int GroupId, int TrackId), TrackFadeState> _fadeStates = new();
    private readonly Dictionary<(int GroupId, int TrackId, NetUserId UserId), PlayerStreamFadeState> _playerFadeStates = new();

    private const float TargetFadeDuration = 1.5f;

    private int _nextGroupId = 1;
    private float _syncAccumulator;

    public event Action? StateChanged;

    public IReadOnlyCollection<AudioGroup> Groups =>
        _groups.Values;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessFades();
        ProcessPlayerFades();

        _syncAccumulator += frameTime;

        if (_syncAccumulator < 1f)
            return;

        _syncAccumulator = 0f;
        SyncAllGroupStreams();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        StopAllStreams();

        _groups.Clear();
        _playbackStates.Clear();
        _fadeStates.Clear();
        _playerFadeStates.Clear();
        _nextGroupId = 1;

        RaiseStateChanged();
    }

    public AudioGroup CreateGroup(
        string name,
        AudioGroupTarget target)
    {
        var id = _nextGroupId;
        _nextGroupId++;

        var group = new AudioGroup(
            id,
            name,
            target);

        _groups.Add(id, group);

        RaiseStateChanged();

        return group;
    }

    public bool TryGetGroup(
        int id,
        [NotNullWhen(true)] out AudioGroup? group)
    {
        return _groups.TryGetValue(id, out group);
    }

    public bool TryRemoveGroup(int id)
    {
        if (!TryGetGroup(id, out var group))
            return false;

        StopGroupStreams(group);

        if (!_groups.Remove(id))
            return false;

        RaiseStateChanged();
        return true;
    }

    public bool TryRenameGroup(
        int id,
        string name)
    {
        if (!TryGetGroup(id, out var group))
            return false;

        group.Rename(name);

        RaiseStateChanged();
        return true;
    }

    public bool TryChangeGroupTarget(
        int id,
        AudioGroupTarget target)
    {
        if (!TryGetGroup(id, out var group))
            return false;

        ApplyTargetChange(
            group,
            target);

        RaiseStateChanged();
        return true;
    }

    public bool TryUpdateGroup(
        int id,
        string name,
        AudioGroupTarget target)
    {
        if (!TryGetGroup(id, out var group))
            return false;

        group.Rename(name);

        ApplyTargetChange(
            group,
            target);

        RaiseStateChanged();
        return true;
    }

    public bool TryAddTrack(
        int groupId,
        string name,
        string path,
        float volume,
        bool loop,
        [NotNullWhen(true)] out AudioTrack? track,
        out string error)
    {
        track = null;

        if (!TryGetGroup(groupId, out var group))
        {
            error = "Group does not exist.";
            return false;
        }

        if (!TryValidateTrackData(
                name,
                path,
                volume,
                out error))
        {
            return false;
        }

        if (!TryGetTrackLength(
                path,
                out var length,
                out error))
        {
            return false;
        }

        track = group.AddTrack(
            name,
            path,
            volume,
            loop,
            length);

        track.SetPaused(true);

        if (!TryStartTrack(
                group,
                track,
                out error))
        {
            group.TryRemoveTrack(track.Id);
            StopTrackStreams(group.Id, track.Id);
            track = null;

            return false;
        }

        RaiseStateChanged();
        return true;
    }

    public bool TryRemoveTrack(
        int groupId,
        int trackId)
    {
        if (!TryGetGroup(groupId, out var group))
            return false;

        StopTrackStreams(group.Id, trackId);

        if (!group.TryRemoveTrack(trackId))
            return false;

        RaiseStateChanged();
        return true;
    }

    public bool TryUpdateTrack(
        int groupId,
        int trackId,
        float volume,
        bool loop,
        out string error)
    {
        if (!TryGetGroup(groupId, out var group))
        {
            error = "Group does not exist.";
            return false;
        }

        if (!group.TryGetTrack(
                trackId,
                out var track))
        {
            error = "Track does not exist.";
            return false;
        }

        if (float.IsNaN(volume)
            || float.IsInfinity(volume)
            || volume < 0f
            || volume > 1f)
        {
            error = "Volume must be between 0 and 1.";
            return false;
        }

        track.ChangeVolume(volume);
        track.ChangeLoop(loop);

        UpdateTrackStreams(
            group.Id,
            track.Id,
            volume);

        RaiseStateChanged();

        error = string.Empty;
        return true;
    }

    public bool TrySetTrackPaused(
        int groupId,
        int trackId,
        bool paused,
        out string error)
    {
        if (!TryGetGroup(groupId, out var group))
        {
            error = "Group does not exist.";
            return false;
        }

        if (!group.TryGetTrack(
                trackId,
                out var track))
        {
            error = "Track does not exist.";
            return false;
        }

        if (track.Paused == paused)
        {
            error = string.Empty;
            return true;
        }

        track.SetPaused(paused);

        var streamKey = (group.Id, track.Id);

        if (!_playbackStates.TryGetValue(
                streamKey,
                out var playbackState))
        {
            playbackState = new TrackPlaybackState(
                _timing.CurTime);

            _playbackStates[streamKey] = playbackState;
        }

        if (paused)
        {
            playbackState.PausedAt = _timing.CurTime;

            foreach (var stream in playbackState.Streams.Values)
            {
                ApplyTrackPlaybackToStream(
                    track,
                    playbackState,
                    stream.Stream);
            }
        }
        else
        {
            if (playbackState.PausedAt != null)
            {
                playbackState.StartedAt +=
                    _timing.CurTime - playbackState.PausedAt.Value;

                playbackState.PausedAt = null;
            }

            foreach (var stream in playbackState.Streams.Values)
            {
                ApplyTrackPlaybackToStream(
                    track,
                    playbackState,
                    stream.Stream);
            }

            SyncGroupStreams(group);
        }

        RaiseStateChanged();

        error = string.Empty;
        return true;
    }

    public bool TryFadeTrack(
        int groupId,
        int trackId,
        float duration,
        bool fadeIn,
        out string error)
    {
        if (!TryGetGroup(groupId, out var group))
        {
            error = "Group does not exist.";
            return false;
        }

        if (!group.TryGetTrack(
                trackId,
                out var track))
        {
            error = "Track does not exist.";
            return false;
        }

        if (float.IsNaN(duration)
            || float.IsInfinity(duration)
            || duration <= 0f
            || duration > 120f)
        {
            error = "Fade duration must be between 0 and 120 seconds.";
            return false;
        }

        var streamKey = (group.Id, track.Id);

        if (!_playbackStates.TryGetValue(
                streamKey,
                out var playbackState))
        {
            playbackState = new TrackPlaybackState(
                _timing.CurTime);

            _playbackStates[streamKey] = playbackState;
        }

        _fadeStates.Remove(streamKey);

        if (fadeIn)
        {
            track.SetPaused(false);

            if (playbackState.PausedAt != null)
            {
                playbackState.StartedAt +=
                    _timing.CurTime - playbackState.PausedAt.Value;

                playbackState.PausedAt = null;
            }

            SyncGroupStreams(group);

            foreach (var stream in playbackState.Streams.Values)
            {
                if (!Exists(stream.Stream))
                    continue;

                SetStreamGain(
                    stream,
                    0f);

                _audio.SetState(
                    stream.Stream,
                    AudioState.Playing);
            }

            _fadeStates[streamKey] = new TrackFadeState(
                _timing.CurTime,
                duration,
                0f,
                track.Volume,
                false);
        }
        else
        {
            if (track.Paused)
            {
                error = "Track is already paused.";
                return false;
            }

            _fadeStates[streamKey] = new TrackFadeState(
                _timing.CurTime,
                duration,
                track.Volume,
                0f,
                true);
        }

        RaiseStateChanged();

        error = string.Empty;
        return true;
    }

    private void ProcessFades()
    {
        foreach (var entry in _fadeStates.ToArray())
        {
            var streamKey = entry.Key;
            var fadeState = entry.Value;

            if (!_playbackStates.TryGetValue(
                    streamKey,
                    out var playbackState))
            {
                _fadeStates.Remove(streamKey);
                continue;
            }

            if (!TryGetGroup(streamKey.GroupId, out var group)
                || !group.TryGetTrack(streamKey.TrackId, out var track))
            {
                _fadeStates.Remove(streamKey);
                continue;
            }

            var elapsed = (float) (_timing.CurTime - fadeState.StartedAt).TotalSeconds;
            var progress = Math.Clamp(
                elapsed / fadeState.Duration,
                0f,
                1f);

            var gain = MathHelper.Lerp(
                fadeState.FromGain,
                fadeState.ToGain,
                progress);

            foreach (var stream in playbackState.Streams.Values)
            {
                if (!Exists(stream.Stream))
                    continue;

                SetStreamGain(
                    stream,
                    gain);
            }

            if (progress < 1f)
                continue;

            _fadeStates.Remove(streamKey);

            if (!fadeState.PauseWhenFinished)
                continue;

            track.SetPaused(true);
            playbackState.PausedAt = _timing.CurTime;

            foreach (var stream in playbackState.Streams.Values)
            {
                if (!Exists(stream.Stream))
                    continue;

                _audio.SetState(
                    stream.Stream,
                    AudioState.Paused);

                SetStreamGain(
                    stream,
                    track.Volume);
            }

            RaiseStateChanged();
        }
    }

    private void ProcessPlayerFades()
    {
        foreach (var entry in _playerFadeStates.ToArray())
        {
            var fadeKey = entry.Key;
            var fadeState = entry.Value;
            var streamKey = (fadeKey.GroupId, fadeKey.TrackId);

            if (!_playbackStates.TryGetValue(
                    streamKey,
                    out var playbackState))
            {
                _playerFadeStates.Remove(fadeKey);
                continue;
            }

            if (!playbackState.Streams.TryGetValue(
                    fadeKey.UserId,
                    out var playerStream)
                || !Exists(playerStream.Stream))
            {
                _playerFadeStates.Remove(fadeKey);
                continue;
            }

            var elapsed = (float) (_timing.CurTime - fadeState.StartedAt).TotalSeconds;
            var progress = Math.Clamp(
                elapsed / fadeState.Duration,
                0f,
                1f);

            var gain = MathHelper.Lerp(
                fadeState.FromGain,
                fadeState.ToGain,
                progress);

            SetStreamGain(
                playerStream,
                gain);

            if (progress < 1f)
                continue;

            _playerFadeStates.Remove(fadeKey);

            if (!fadeState.StopWhenFinished)
                continue;

            _audio.Stop(playerStream.Stream);
            playbackState.Streams.Remove(fadeKey.UserId);
        }
    }

    private void StartPlayerStreamFade(
        (int GroupId, int TrackId) streamKey,
        NetUserId userId,
        PlayerTrackStream playerStream,
        float fromGain,
        float toGain,
        bool stopWhenFinished)
    {
        var fadeKey = (streamKey.GroupId, streamKey.TrackId, userId);

        _playerFadeStates[fadeKey] = new PlayerStreamFadeState(
            _timing.CurTime,
            TargetFadeDuration,
            fromGain,
            toGain,
            stopWhenFinished);

        SetStreamGain(
            playerStream,
            fromGain);
    }

    private float GetCurrentTrackGain(
        (int GroupId, int TrackId) streamKey,
        AudioTrack track)
    {
        if (!_fadeStates.TryGetValue(
                streamKey,
                out var fadeState))
        {
            return track.Volume;
        }

        var elapsed = (float) (_timing.CurTime - fadeState.StartedAt).TotalSeconds;
        var progress = Math.Clamp(
            elapsed / fadeState.Duration,
            0f,
            1f);

        return MathHelper.Lerp(
            fadeState.FromGain,
            fadeState.ToGain,
            progress);
    }

    private void SetStreamGain(
        PlayerTrackStream playerStream,
        float gain)
    {
        gain = Math.Clamp(
            gain,
            0f,
            1f);

        playerStream.CurrentGain = gain;

        if (!Exists(playerStream.Stream))
            return;

        _audio.SetGain(
            playerStream.Stream,
            gain);
    }

    public float GetTrackPlaybackPosition(
        int groupId,
        AudioTrack track)
    {
        var streamKey = (groupId, track.Id);

        if (!_playbackStates.TryGetValue(
                streamKey,
                out var playbackState))
        {
            return 0f;
        }

        return playbackState.GetPlaybackPosition(
            _timing.CurTime,
            track.Length,
            track.Loop);
    }

    public bool TrySetTrackTime(
        int groupId,
        int trackId,
        float time,
        out string error)
    {
        if (!TryGetGroup(groupId, out var group))
        {
            error = "Group does not exist.";
            return false;
        }

        if (!group.TryGetTrack(
                trackId,
                out var track))
        {
            error = "Track does not exist.";
            return false;
        }

        if (float.IsNaN(time)
            || float.IsInfinity(time))
        {
            error = "Invalid playback time.";
            return false;
        }

        time = Math.Clamp(
            time,
            0f,
            track.Length);

        var streamKey = (group.Id, track.Id);

        if (!_playbackStates.TryGetValue(
                streamKey,
                out var playbackState))
        {
            playbackState = new TrackPlaybackState(
                _timing.CurTime - TimeSpan.FromSeconds(time));

            _playbackStates[streamKey] = playbackState;
        }

        if (track.Paused)
        {
            playbackState.PausedAt ??= _timing.CurTime;
            playbackState.StartedAt =
                playbackState.PausedAt.Value - TimeSpan.FromSeconds(time);
        }
        else
        {
            playbackState.StartedAt =
                _timing.CurTime - TimeSpan.FromSeconds(time);
        }

        foreach (var stream in playbackState.Streams.Values)
        {
            ApplyTrackPlaybackToStream(
                track,
                playbackState,
                stream.Stream);
        }

        RaiseStateChanged();

        error = string.Empty;
        return true;
    }

    private bool TryGetTrackLength(
        string path,
        out float length,
        out string error)
    {
        length = 0f;

        try
        {
            var sound = new SoundPathSpecifier(
                new ResPath(path));

            var resolved = _audio.ResolveSound(sound);
            length = (float) _audio.GetAudioLength(resolved).TotalSeconds;
        }
        catch
        {
            error = "Failed to read audio length.";
            return false;
        }

        if (length <= 0f)
        {
            error = "Audio length is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ApplyTargetChange(
        AudioGroup group,
        AudioGroupTarget newTarget)
    {
        group.ChangeTarget(newTarget);

        SyncGroupStreams(group);
    }

    private void SyncAllGroupStreams()
    {
        if (_groups.Count == 0 || _playbackStates.Count == 0)
            return;

        BuildPlayerLocationCache(
            out var playersByMap,
            out var playersByGrid);

        foreach (var group in _groups.Values)
        {
            SyncGroupStreams(
                group,
                playersByMap,
                playersByGrid);
        }
    }

    private void SyncGroupStreams(AudioGroup group)
    {
        BuildPlayerLocationCache(
            out var playersByMap,
            out var playersByGrid);

        SyncGroupStreams(
            group,
            playersByMap,
            playersByGrid);
    }

    private void SyncGroupStreams(
        AudioGroup group,
        Dictionary<MapId, HashSet<NetUserId>> playersByMap,
        Dictionary<EntityUid, HashSet<NetUserId>> playersByGrid)
    {
        var targetPlayers = GetTargetPlayers(
            group,
            playersByMap,
            playersByGrid);

        foreach (var track in group.Tracks)
        {
            SyncPlayersTrackStreams(
                group,
                track,
                targetPlayers);
        }
    }

    private HashSet<NetUserId> GetTargetPlayers(AudioGroup group)
    {
        BuildPlayerLocationCache(
            out var playersByMap,
            out var playersByGrid);

        return GetTargetPlayers(
            group,
            playersByMap,
            playersByGrid);
    }

    private HashSet<NetUserId> GetTargetPlayers(
        AudioGroup group,
        Dictionary<MapId, HashSet<NetUserId>> playersByMap,
        Dictionary<EntityUid, HashSet<NetUserId>> playersByGrid)
    {
        return group.Target switch
        {
            PlayersAudioGroupTarget playersTarget =>
                playersTarget.Players.ToHashSet(),

            MapAudioGroupTarget mapTarget
                when playersByMap.TryGetValue(mapTarget.MapId, out var players) =>
                players,

            GridAudioGroupTarget gridTarget
                when playersByGrid.TryGetValue(gridTarget.GridUid, out var players) =>
                players,

            _ => new HashSet<NetUserId>()
        };
    }

    private void BuildPlayerLocationCache(
        out Dictionary<MapId, HashSet<NetUserId>> playersByMap,
        out Dictionary<EntityUid, HashSet<NetUserId>> playersByGrid)
    {
        playersByMap = new Dictionary<MapId, HashSet<NetUserId>>();
        playersByGrid = new Dictionary<EntityUid, HashSet<NetUserId>>();

        foreach (var session in _playerManager.Sessions)
        {
            var attached = session.AttachedEntity;

            if (attached == null
                || !Exists(attached.Value))
            {
                continue;
            }

            var xform = Transform(attached.Value);

            if (!playersByMap.TryGetValue(
                    xform.MapID,
                    out var mapPlayers))
            {
                mapPlayers = new HashSet<NetUserId>();
                playersByMap[xform.MapID] = mapPlayers;
            }

            mapPlayers.Add(session.UserId);

            if (xform.GridUid == null)
                continue;

            var gridUid = xform.GridUid.Value;

            if (!playersByGrid.TryGetValue(
                    gridUid,
                    out var gridPlayers))
            {
                gridPlayers = new HashSet<NetUserId>();
                playersByGrid[gridUid] = gridPlayers;
            }

            gridPlayers.Add(session.UserId);
        }
    }

    private void SyncPlayersTrackStreams(
        AudioGroup group,
        AudioTrack track,
        HashSet<NetUserId> targetPlayers)
    {
        var streamKey = (group.Id, track.Id);

        if (!_playbackStates.TryGetValue(
                streamKey,
                out var playbackState))
        {
            playbackState = new TrackPlaybackState(
                _timing.CurTime);

            _playbackStates[streamKey] = playbackState;
        }

        var finished =
            !track.Loop
            && !track.Paused
            && playbackState.GetPlaybackPosition(
                _timing.CurTime,
                track.Length,
                track.Loop) >= track.Length;

        if (finished)
            return;

        foreach (var entry in playbackState.Streams.ToArray())
        {
            var userId = entry.Key;
            var playerStream = entry.Value;
            var fadeKey = (group.Id, track.Id, userId);

            var valid =
                TryGetSession(userId, out var currentSession)
                && ReferenceEquals(playerStream.Session, currentSession)
                && Exists(playerStream.Stream);

            if (!valid)
            {
                _playerFadeStates.Remove(fadeKey);
                playbackState.Streams.Remove(userId);
                continue;
            }

            if (targetPlayers.Contains(userId))
                continue;

            if (track.Paused)
            {
                _audio.Stop(playerStream.Stream);
                _playerFadeStates.Remove(fadeKey);
                playbackState.Streams.Remove(userId);
                continue;
            }

            if (_playerFadeStates.TryGetValue(
                    fadeKey,
                    out var existingFade)
                && existingFade.StopWhenFinished)
            {
                continue;
            }

            StartPlayerStreamFade(
                streamKey,
                userId,
                playerStream,
                playerStream.CurrentGain,
                0f,
                true);
        }

        foreach (var userId in targetPlayers)
        {
            if (!TryGetSession(
                    userId,
                    out var session))
            {
                continue;
            }

            var fadeKey = (group.Id, track.Id, userId);

            if (playbackState.Streams.TryGetValue(
                    userId,
                    out var existing)
                && ReferenceEquals(existing.Session, session)
                && Exists(existing.Stream))
            {
                if (_playerFadeStates.TryGetValue(
                        fadeKey,
                        out var existingFade)
                    && existingFade.StopWhenFinished)
                {
                    StartPlayerStreamFade(
                        streamKey,
                        userId,
                        existing,
                        existing.CurrentGain,
                        GetCurrentTrackGain(
                            streamKey,
                            track),
                        false);
                }

                continue;
            }

            if (track.Paused)
                continue;

            if (!TryStartPlayerStream(
                    track,
                    session,
                    out var stream,
                    0f))
            {
                continue;
            }

            var playerStream = new PlayerTrackStream(
                session,
                stream,
                0f);

            playbackState.Streams[userId] = playerStream;

            ApplyTrackPlaybackToStream(
                track,
                playbackState,
                stream);

            StartPlayerStreamFade(
                streamKey,
                userId,
                playerStream,
                0f,
                GetCurrentTrackGain(
                    streamKey,
                    track),
                false);
        }
    }

    private bool TryValidateTrackData(
        string name,
        string path,
        float volume,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Track name cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Track path cannot be empty.";
            return false;
        }

        if (!path.StartsWith('/'))
        {
            error = "Track path must start with '/'.";
            return false;
        }

        if (!path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            error = "Track path must point to an .ogg file.";
            return false;
        }

        if (float.IsNaN(volume)
            || float.IsInfinity(volume)
            || volume < 0f
            || volume > 1f)
        {
            error = "Volume must be between 0 and 1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void StartGroupStreams(AudioGroup group)
    {
        foreach (var track in group.Tracks)
        {
            TryStartTrack(
                group,
                track,
                out _);
        }
    }

    private bool TryStartTrack(
        AudioGroup group,
        AudioTrack track,
        out string error)
    {
        error = string.Empty;

        var targetPlayers = GetTargetPlayers(group);
        var streamKey = (group.Id, track.Id);

        StopTrackStreams(
            group.Id,
            track.Id);

        var playbackState = new TrackPlaybackState(
            _timing.CurTime);

        if (track.Paused)
        {
            playbackState.PausedAt = _timing.CurTime;
            _playbackStates[streamKey] = playbackState;

            return true;
        }

        foreach (var userId in targetPlayers)
        {
            if (!TryGetSession(
                    userId,
                    out var session))
            {
                continue;
            }

            if (!TryStartPlayerStream(
                    track,
                    session,
                    out var stream))
            {
                continue;
            }

            playbackState.Streams[userId] = new PlayerTrackStream(
                session,
                stream,
                track.Volume);

            ApplyTrackPlaybackToStream(
                track,
                playbackState,
                stream);
        }

        _playbackStates[streamKey] = playbackState;
        return true;
    }

    private bool TryStartPlayerStream(
        AudioTrack track,
        ICommonSession session,
        out EntityUid stream,
        float? initialGain = null)
    {
        stream = default;

        var sound = new SoundPathSpecifier(
            new ResPath(track.Path));

        var gain = initialGain ?? track.Volume;

        var audioParams = AudioParams.Default
            .WithVolume(SharedAudioSystem.GainToVolume(gain))
            .WithLoop(track.Loop);

        var result = _audio.PlayGlobal(
            sound,
            session,
            audioParams);

        if (result == null)
            return false;

        stream = result.Value.Entity;
        return true;
    }

    private bool TryGetSession(
        NetUserId userId,
        [NotNullWhen(true)] out ICommonSession? session)
    {
        foreach (var candidate in _playerManager.Sessions)
        {
            if (candidate.UserId != userId)
                continue;

            session = candidate;
            return true;
        }

        session = null;
        return false;
    }

    private void RestartTrackInternal(
        AudioGroup group,
        AudioTrack track)
    {
        StopTrackStreams(
            group.Id,
            track.Id);

        TryStartTrack(
            group,
            track,
            out _);
    }

    private void UpdateTrackStreams(
        int groupId,
        int trackId,
        float volume)
    {
        var streamKey = (groupId, trackId);

        if (!_playbackStates.TryGetValue(
                streamKey,
                out var playbackState))
        {
            return;
        }

        foreach (var stream in playbackState.Streams.Values)
        {
            SetStreamGain(
                stream,
                volume);
        }
    }

    private void StopGroupStreams(AudioGroup group)
    {
        foreach (var track in group.Tracks)
        {
            StopTrackStreams(
                group.Id,
                track.Id);
        }
    }

    private void StopTrackStreams(
        int groupId,
        int trackId)
    {
        _fadeStates.Remove((groupId, trackId));

        foreach (var key in _playerFadeStates.Keys.ToArray())
        {
            if (key.GroupId == groupId
                && key.TrackId == trackId)
            {
                _playerFadeStates.Remove(key);
            }
        }

        var streamKey = (groupId, trackId);

        if (!_playbackStates.Remove(
                streamKey,
                out var playbackState))
        {
            return;
        }

        StopPlaybackState(playbackState);
    }

    private void StopAllStreams()
    {
        foreach (var playbackState in _playbackStates.Values)
        {
            StopPlaybackState(playbackState);
        }

        _playbackStates.Clear();
        _fadeStates.Clear();
        _playerFadeStates.Clear();
    }

    private void StopPlaybackState(
        TrackPlaybackState playbackState)
    {
        foreach (var stream in playbackState.Streams.Values)
        {
            _audio.Stop(stream.Stream);
        }

        playbackState.Streams.Clear();
    }

    private void ApplyTrackPlaybackToStream(
        AudioTrack track,
        TrackPlaybackState playbackState,
        EntityUid stream)
    {
        var position = playbackState.GetPlaybackPosition(
            _timing.CurTime,
            track.Length,
            track.Loop);

        if (track.Paused)
        {
            playbackState.PausedAt ??= _timing.CurTime;

            _audio.SetState(
                stream,
                AudioState.Paused);

            _audio.SetPlaybackPosition(
                stream,
                position);

            _audio.SetState(
                stream,
                AudioState.Paused);

            return;
        }

        _audio.SetPlaybackPosition(
            stream,
            position);

        _audio.SetState(
            stream,
            AudioState.Playing);
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke();
    }

    private sealed class TrackPlaybackState
    {
        public TimeSpan StartedAt { get; set; }

        public TimeSpan? PausedAt { get; set; }

        public Dictionary<NetUserId, PlayerTrackStream> Streams { get; } = new();

        public TrackPlaybackState(TimeSpan startedAt)
        {
            StartedAt = startedAt;
        }

        public float GetPlaybackPosition(
            TimeSpan currentTime,
            float length,
            bool loop)
        {
            var time = PausedAt ?? currentTime;
            var position = (float) (time - StartedAt).TotalSeconds;

            if (length <= 0f)
                return 0f;

            if (loop)
                return position % length;

            return Math.Clamp(
                position,
                0f,
                length);
        }
    }

    private sealed class PlayerTrackStream
    {
        public ICommonSession Session { get; }

        public EntityUid Stream { get; }

        public float CurrentGain { get; set; }

        public PlayerTrackStream(
            ICommonSession session,
            EntityUid stream,
            float currentGain)
        {
            Session = session;
            Stream = stream;
            CurrentGain = currentGain;
        }
    }

    private sealed class TrackFadeState
    {
        public TimeSpan StartedAt { get; }

        public float Duration { get; }

        public float FromGain { get; }

        public float ToGain { get; }

        public bool PauseWhenFinished { get; }

        public TrackFadeState(
            TimeSpan startedAt,
            float duration,
            float fromGain,
            float toGain,
            bool pauseWhenFinished)
        {
            StartedAt = startedAt;
            Duration = duration;
            FromGain = fromGain;
            ToGain = toGain;
            PauseWhenFinished = pauseWhenFinished;
        }
    }

    private sealed class PlayerStreamFadeState
    {
        public TimeSpan StartedAt { get; }

        public float Duration { get; }

        public float FromGain { get; }

        public float ToGain { get; }

        public bool StopWhenFinished { get; }

        public PlayerStreamFadeState(
            TimeSpan startedAt,
            float duration,
            float fromGain,
            float toGain,
            bool stopWhenFinished)
        {
            StartedAt = startedAt;
            Duration = duration;
            FromGain = fromGain;
            ToGain = toGain;
            StopWhenFinished = stopWhenFinished;
        }
    }
}
