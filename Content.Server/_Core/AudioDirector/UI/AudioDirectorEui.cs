using System.Linq;
using Content.Server._Core.AudioDirector.Targets;
using Content.Server.EUI;
using Content.Shared._Core.AudioDirector.Eui;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;

namespace Content.Server._Core.AudioDirector.UI;

[UsedImplicitly]
public sealed class AudioDirectorEui : BaseEui
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly AudioDirectorSystem _audioDirectorSystem;
    private readonly SharedMapSystem _map;

    public AudioDirectorEui()
    {
        IoCManager.InjectDependencies(this);

        var entitySystemManager = IoCManager
            .Resolve<IEntitySystemManager>();

        _audioDirectorSystem = entitySystemManager
            .GetEntitySystem<AudioDirectorSystem>();

        _map = entitySystemManager
            .GetEntitySystem<SharedMapSystem>();
    }

    public override void Opened()
    {
        base.Opened();

        _audioDirectorSystem.StateChanged +=
            OnAudioDirectorStateChanged;

        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();

        _audioDirectorSystem.StateChanged -=
            OnAudioDirectorStateChanged;
    }

    public override EuiStateBase GetNewState()
    {
        var groups = _audioDirectorSystem.Groups
            .Select(CreateGroupEuiData)
            .ToArray();

        var players = _playerManager.Sessions
            .OrderBy(session => session.Name)
            .Select(session => new AudioDirectorPlayerEuiData(
                session.UserId,
                session.Name))
            .ToArray();

        return new AudioDirectorEuiState(
            groups,
            players);
    }

    private void OnAudioDirectorStateChanged()
    {
        StateDirty();
    }

    private AudioGroupEuiData CreateGroupEuiData(
        AudioGroup group)
    {
        string? gridNetEntityId = null;
        string? mapId = null;
        NetUserId[] players = [];

        switch (group.Target)
        {
            case GridAudioGroupTarget gridTarget:
            {
                var netEntity = _entities.GetNetEntity(
                    gridTarget.GridUid);

                gridNetEntityId = netEntity.ToString();
                break;
            }

            case MapAudioGroupTarget mapTarget:
            {
                mapId = mapTarget.MapId.ToString();
                break;
            }

            case PlayersAudioGroupTarget playersTarget:
            {
                players = playersTarget.Players.ToArray();
                break;
            }
        }

        var tracks = group.Tracks
            .Select(track => new AudioTrackEuiData(
                track.Id,
                track.Name,
                track.Path,
                track.Volume,
                track.Loop,
                track.Paused,
                track.Length,
                _audioDirectorSystem.GetTrackPlaybackPosition(
                    group.Id,
                    track)))
            .ToArray();

        return new AudioGroupEuiData(
            group.Id,
            group.Name,
            group.Target.Type,
            gridNetEntityId,
            mapId,
            players,
            tracks);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case AudioDirectorEuiMsg.CreateGridGroupRequest request:
                TryCreateGridGroup(request);
                break;

            case AudioDirectorEuiMsg.CreateMapGroupRequest request:
                TryCreateMapGroup(request);
                break;

            case AudioDirectorEuiMsg.CreatePlayersGroupRequest request:
                TryCreatePlayersGroup(request);
                break;

            case AudioDirectorEuiMsg.DeleteGroupRequest request:
                TryDeleteGroup(request);
                break;

            case AudioDirectorEuiMsg.UpdateGridGroupRequest request:
                TryUpdateGridGroup(request);
                break;

            case AudioDirectorEuiMsg.UpdateMapGroupRequest request:
                TryUpdateMapGroup(request);
                break;

            case AudioDirectorEuiMsg.UpdatePlayersGroupRequest request:
                TryUpdatePlayersGroup(request);
                break;

            case AudioDirectorEuiMsg.AddTrackRequest request:
                TryAddTrack(request);
                break;

            case AudioDirectorEuiMsg.DeleteTrackRequest request:
                TryDeleteTrack(request);
                break;

            case AudioDirectorEuiMsg.UpdateTrackRequest request:
                TryUpdateTrack(request);
                break;

            case AudioDirectorEuiMsg.SetTrackPausedRequest request:
                TrySetTrackPaused(request);
                break;

            case AudioDirectorEuiMsg.SetTrackTimeRequest request:
                TrySetTrackTime(request);
                break;

            case AudioDirectorEuiMsg.FadeTrackRequest request:
                TryFadeTrack(request);
                break;
        }
    }

    private void TryCreateGridGroup(
        AudioDirectorEuiMsg.CreateGridGroupRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    "Group name cannot be empty."));
            return;
        }

        if (!NetEntity.TryParse(
                request.GridNetEntityId,
                out var gridNet))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    "Invalid Grid UID."));
            return;
        }

        if (!_entities.TryGetEntity(
                gridNet,
                out var gridUid))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    "Grid does not exist."));
            return;
        }

        if (!_entities.HasComponent<MapGridComponent>(gridUid))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    "Entity is not a grid."));
            return;
        }

        _audioDirectorSystem.CreateGroup(
            name,
            new GridAudioGroupTarget(gridUid.Value));

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup));
    }

    private void TryCreateMapGroup(
        AudioDirectorEuiMsg.CreateMapGroupRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    "Group name cannot be empty."));
            return;
        }

        var mapId = new MapId(request.MapId);

        if (!_map.MapExists(mapId))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    "Map does not exist."));
            return;
        }

        _audioDirectorSystem.CreateGroup(
            name,
            new MapAudioGroupTarget(mapId));

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup));
    }

    private void TryCreatePlayersGroup(
        AudioDirectorEuiMsg.CreatePlayersGroupRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    "Group name cannot be empty."));
            return;
        }

        if (!TryValidatePlayers(
                request.Players,
                out var players,
                out var error))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup,
                    error));
            return;
        }

        _audioDirectorSystem.CreateGroup(
            name,
            new PlayersAudioGroupTarget(players));

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup));
    }

    private void TryDeleteGroup(
        AudioDirectorEuiMsg.DeleteGroupRequest request)
    {
        if (!_audioDirectorSystem.TryRemoveGroup(request.GroupId))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.DeleteGroup,
                    "Group does not exist."));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.DeleteGroup));
    }

    private void TryUpdateGridGroup(
        AudioDirectorEuiMsg.UpdateGridGroupRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group name cannot be empty."));
            return;
        }

        if (!_audioDirectorSystem.TryGetGroup(
                request.GroupId,
                out var group))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group does not exist."));
            return;
        }

        if (group.Target is not GridAudioGroupTarget)
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group is not a Grid group."));
            return;
        }

        if (!NetEntity.TryParse(
                request.GridNetEntityId,
                out var gridNet))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Invalid Grid UID."));
            return;
        }

        if (!_entities.TryGetEntity(
                gridNet,
                out var gridUid))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Grid does not exist."));
            return;
        }

        if (!_entities.HasComponent<MapGridComponent>(gridUid))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Entity is not a grid."));
            return;
        }

        if (!_audioDirectorSystem.TryUpdateGroup(
                request.GroupId,
                name,
                new GridAudioGroupTarget(gridUid.Value)))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Failed to update group."));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup));
    }

    private void TryUpdateMapGroup(
        AudioDirectorEuiMsg.UpdateMapGroupRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group name cannot be empty."));
            return;
        }

        if (!_audioDirectorSystem.TryGetGroup(
                request.GroupId,
                out var group))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group does not exist."));
            return;
        }

        if (group.Target is not MapAudioGroupTarget)
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group is not a Map group."));
            return;
        }

        var mapId = new MapId(request.MapId);

        if (!_map.MapExists(mapId))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Map does not exist."));
            return;
        }

        if (!_audioDirectorSystem.TryUpdateGroup(
                request.GroupId,
                name,
                new MapAudioGroupTarget(mapId)))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Failed to update group."));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup));
    }

    private void TryUpdatePlayersGroup(
        AudioDirectorEuiMsg.UpdatePlayersGroupRequest request)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group name cannot be empty."));
            return;
        }

        if (!_audioDirectorSystem.TryGetGroup(
                request.GroupId,
                out var group))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group does not exist."));
            return;
        }

        if (group.Target is not PlayersAudioGroupTarget)
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Group is not a Players group."));
            return;
        }

        if (!TryValidatePlayers(
                request.Players,
                out var players,
                out var error))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    error));
            return;
        }

        if (!_audioDirectorSystem.TryUpdateGroup(
                request.GroupId,
                name,
                new PlayersAudioGroupTarget(players)))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup,
                    "Failed to update group."));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup));
    }

    private void TryAddTrack(
        AudioDirectorEuiMsg.AddTrackRequest request)
    {
        var name = request.Name.Trim();
        var path = request.Path.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.AddTrack,
                    "Track name cannot be empty."));
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.AddTrack,
                    "Track path cannot be empty."));
            return;
        }

        if (float.IsNaN(request.Volume)
            || float.IsInfinity(request.Volume)
            || request.Volume < 0f
            || request.Volume > 1f)
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.AddTrack,
                    "Volume must be between 0 and 1."));
            return;
        }

        if (!_audioDirectorSystem.TryAddTrack(
                request.GroupId,
                name,
                path,
                request.Volume,
                request.Loop,
                out _,
                out var error))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.AddTrack,
                    error));
            return;
        }


        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.AddTrack));
    }

    private void TryDeleteTrack(
        AudioDirectorEuiMsg.DeleteTrackRequest request)
    {
        if (!_audioDirectorSystem.TryRemoveTrack(
                request.GroupId,
                request.TrackId))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.DeleteTrack,
                    "Track does not exist."));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.DeleteTrack));
    }

    private void TryUpdateTrack(
        AudioDirectorEuiMsg.UpdateTrackRequest request)
    {
        if (!_audioDirectorSystem.TryUpdateTrack(
                request.GroupId,
                request.TrackId,
                request.Volume,
                request.Loop,
                out var error))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.UpdateTrack,
                    error));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.UpdateTrack));
    }

    private void TrySetTrackPaused(
        AudioDirectorEuiMsg.SetTrackPausedRequest request)
    {
        if (!_audioDirectorSystem.TrySetTrackPaused(
                request.GroupId,
                request.TrackId,
                request.Paused,
                out var error))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.PauseTrack,
                    error));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.PauseTrack));
    }

    private void TrySetTrackTime(
        AudioDirectorEuiMsg.SetTrackTimeRequest request)
    {
        if (!_audioDirectorSystem.TrySetTrackTime(
                request.GroupId,
                request.TrackId,
                request.Time,
                out var error))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.SetTrackTime,
                    error));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.SetTrackTime));
    }

    private void TryFadeTrack(
        AudioDirectorEuiMsg.FadeTrackRequest request)
    {
        if (!_audioDirectorSystem.TryFadeTrack(
                request.GroupId,
                request.TrackId,
                request.Duration,
                request.FadeIn,
                out var error))
        {
            SendMessage(
                new AudioDirectorEuiMsg.OperationError(
                    AudioDirectorEuiMsg.AudioDirectorOperation.FadeTrack,
                    error));
            return;
        }

        SendMessage(
            new AudioDirectorEuiMsg.OperationSuccess(
                AudioDirectorEuiMsg.AudioDirectorOperation.FadeTrack));
    }

    private bool TryValidatePlayers(
        NetUserId[] requestedPlayers,
        out NetUserId[] players,
        out string error)
    {
        players = requestedPlayers
            .Distinct()
            .ToArray();

        if (players.Length == 0)
        {
            error = "Select at least one player.";
            return false;
        }

        var onlinePlayers = _playerManager.Sessions
            .Select(session => session.UserId)
            .ToHashSet();

        foreach (var player in players)
        {
            if (onlinePlayers.Contains(player))
                continue;

            error = "One or more selected players are no longer online.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
