using System.Linq;
using Content.Client.Eui;
using Content.Shared._Core.AudioDirector.Eui;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.Network;

namespace Content.Client._Core.AudioDirector.UI;

[UsedImplicitly]
public sealed partial class AudioDirectorEui : BaseEui
{
    private readonly AudioDirectorWindow _window;

    private CreateAudioGroupWindow? _createGroupWindow;
    private DeleteAudioGroupWindow? _deleteGroupWindow;
    private EditAudioGroupWindow? _editGroupWindow;
    private AddAudioTrackWindow? _addTrackWindow;

    private AudioGroupEuiData[] _groups = [];
    private AudioDirectorPlayerEuiData[] _players = [];

    public AudioDirectorEui()
    {
        _window = new AudioDirectorWindow();

        _window.OnClose += SendClosedMessage;
        _window.OnAddGroupPressed += OpenCreateGroupWindow;
        _window.OnDeleteGroupPressed += OpenDeleteGroupConfirmation;
        _window.OnEditGroupPressed += OpenEditGroupWindow;
        _window.OnAddTrackPressed += OpenAddTrackWindow;
        _window.OnDeleteTrackPressed += SendDeleteTrackRequest;
        _window.OnUpdateTrackPressed += SendUpdateTrackRequest;
        _window.OnSetTrackPausedPressed += SendSetTrackPausedRequest;
        _window.OnSetTrackTimePressed += SendSetTrackTimeRequest;
        _window.OnFadeTrackPressed += SendFadeTrackRequest;
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        _window.OnClose -= SendClosedMessage;
        _window.OnAddGroupPressed -= OpenCreateGroupWindow;
        _window.OnDeleteGroupPressed -= OpenDeleteGroupConfirmation;
        _window.OnEditGroupPressed -= OpenEditGroupWindow;
        _window.OnAddTrackPressed -= OpenAddTrackWindow;
        _window.OnDeleteTrackPressed -= SendDeleteTrackRequest;
        _window.OnUpdateTrackPressed -= SendUpdateTrackRequest;
        _window.OnSetTrackPausedPressed -= SendSetTrackPausedRequest;
        _window.OnSetTrackTimePressed -= SendSetTrackTimeRequest;
        _window.OnFadeTrackPressed -= SendFadeTrackRequest;

        _window.Close();
    }

    private void OpenCreateGroupWindow()
    {
        if (_createGroupWindow != null)
        {
            _createGroupWindow.MoveToFront();
            return;
        }

        _createGroupWindow = new CreateAudioGroupWindow(
            _players);

        _createGroupWindow.OnCreateGridGroupRequested +=
            SendCreateGridGroupRequest;

        _createGroupWindow.OnCreateMapGroupRequested +=
            SendCreateMapGroupRequest;

        _createGroupWindow.OnCreatePlayersGroupRequested +=
            SendCreatePlayersGroupRequest;

        _createGroupWindow.OnClose += () =>
            _createGroupWindow = null;

        _createGroupWindow.OpenCentered();
    }

    private void OpenEditGroupWindow(int groupId)
    {
        var group = _groups.FirstOrDefault(
            group => group.Id == groupId);

        if (group == null)
            return;

        if (_editGroupWindow != null)
        {
            _editGroupWindow.Close();
            _editGroupWindow = null;
        }

        _editGroupWindow = new EditAudioGroupWindow(
            group,
            _players);

        _editGroupWindow.OnSaveGridRequested +=
            SendUpdateGridGroupRequest;

        _editGroupWindow.OnSaveMapRequested +=
            SendUpdateMapGroupRequest;

        _editGroupWindow.OnSavePlayersRequested +=
            SendUpdatePlayersGroupRequest;

        _editGroupWindow.OnClose += () =>
            _editGroupWindow = null;

        _editGroupWindow.OpenCentered();
    }

    private void OpenAddTrackWindow(int groupId)
    {
        var group = _groups.FirstOrDefault(
            group => group.Id == groupId);

        if (group == null)
            return;

        if (_addTrackWindow != null)
        {
            _addTrackWindow.Close();
            _addTrackWindow = null;
        }

        _addTrackWindow = new AddAudioTrackWindow(
            group.Id,
            group.Name);

        _addTrackWindow.OnAddTrackRequested +=
            SendAddTrackRequest;

        _addTrackWindow.OnClose += () =>
            _addTrackWindow = null;

        _addTrackWindow.OpenCentered();
    }

    private void OpenDeleteGroupConfirmation(int groupId)
    {
        var group = _groups.FirstOrDefault(
            group => group.Id == groupId);

        if (group == null)
            return;

        if (_deleteGroupWindow != null)
        {
            _deleteGroupWindow.Close();
            _deleteGroupWindow = null;
        }

        _deleteGroupWindow = new DeleteAudioGroupWindow(
            group.Id,
            group.Name);

        _deleteGroupWindow.OnDeleteConfirmed +=
            SendDeleteGroupRequest;

        _deleteGroupWindow.OnClose += () =>
            _deleteGroupWindow = null;

        _deleteGroupWindow.OpenCentered();
    }

    private void SendCreateGridGroupRequest(
        string name,
        string gridNetEntityId)
    {
        SendMessage(
            new AudioDirectorEuiMsg.CreateGridGroupRequest(
                name,
                gridNetEntityId));
    }

    private void SendCreateMapGroupRequest(
        string name,
        int mapId)
    {
        SendMessage(
            new AudioDirectorEuiMsg.CreateMapGroupRequest(
                name,
                mapId));
    }

    private void SendCreatePlayersGroupRequest(
        string name,
        NetUserId[] players)
    {
        SendMessage(
            new AudioDirectorEuiMsg.CreatePlayersGroupRequest(
                name,
                players));
    }

    private void SendUpdateGridGroupRequest(
        int groupId,
        string name,
        string gridNetEntityId)
    {
        SendMessage(
            new AudioDirectorEuiMsg.UpdateGridGroupRequest(
                groupId,
                name,
                gridNetEntityId));
    }

    private void SendUpdateMapGroupRequest(
        int groupId,
        string name,
        int mapId)
    {
        SendMessage(
            new AudioDirectorEuiMsg.UpdateMapGroupRequest(
                groupId,
                name,
                mapId));
    }

    private void SendUpdatePlayersGroupRequest(
        int groupId,
        string name,
        NetUserId[] players)
    {
        SendMessage(
            new AudioDirectorEuiMsg.UpdatePlayersGroupRequest(
                groupId,
                name,
                players));
    }

    private void SendAddTrackRequest(
        int groupId,
        string name,
        string path,
        float volume,
        bool loop)
    {
        SendMessage(
            new AudioDirectorEuiMsg.AddTrackRequest(
                groupId,
                name,
                path,
                volume,
                loop));
    }

    private void SendSetTrackTimeRequest(
        int groupId,
        int trackId,
        float time)
    {
        SendMessage(
            new AudioDirectorEuiMsg.SetTrackTimeRequest(
                groupId,
                trackId,
                time));
    }

    private void SendFadeTrackRequest(
        int groupId,
        int trackId,
        float duration,
        bool fadeIn)
    {
        SendMessage(
            new AudioDirectorEuiMsg.FadeTrackRequest(
                groupId,
                trackId,
                duration,
                fadeIn));
    }

    private void SendDeleteTrackRequest(
        int groupId,
        int trackId)
    {
        SendMessage(
            new AudioDirectorEuiMsg.DeleteTrackRequest(
                groupId,
                trackId));
    }

    private void SendUpdateTrackRequest(
        int groupId,
        int trackId,
        float volume,
        bool loop)
    {
        SendMessage(
            new AudioDirectorEuiMsg.UpdateTrackRequest(
                groupId,
                trackId,
                volume,
                loop));
    }

    private void SendSetTrackPausedRequest(
        int groupId,
        int trackId,
        bool paused)
    {
        SendMessage(
            new AudioDirectorEuiMsg.SetTrackPausedRequest(
                groupId,
                trackId,
                paused));
    }

    private void SendRestartTrackRequest(
        int groupId,
        int trackId)
    {
        SendMessage(
            new AudioDirectorEuiMsg.RestartTrackRequest(
                groupId,
                trackId));
    }

    private void SendDeleteGroupRequest(int groupId)
    {
        SendMessage(
            new AudioDirectorEuiMsg.DeleteGroupRequest(
                groupId));
    }

    private void SendClosedMessage()
    {
        SendMessage(new CloseEuiMessage());
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is not AudioDirectorEuiState audioState)
            return;

        _groups = audioState.Groups;
        _players = audioState.Players;

        _window.SetGroups(audioState.Groups);
        _createGroupWindow?.SetPlayers(_players);
        _editGroupWindow?.SetPlayers(_players);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case AudioDirectorEuiMsg.OperationSuccess success:
                HandleOperationSuccess(success);
                break;

            case AudioDirectorEuiMsg.OperationError error:
                HandleOperationError(error);
                break;
        }
    }

    private void HandleOperationSuccess(
        AudioDirectorEuiMsg.OperationSuccess success)
    {
        switch (success.Operation)
        {
            case AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup:
                _createGroupWindow?.Close();
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.DeleteGroup:
                _deleteGroupWindow?.Close();
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup:
                _editGroupWindow?.Close();
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.AddTrack:
                _addTrackWindow?.Close();
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.SetTrackTime:
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.DeleteTrack:
            case AudioDirectorEuiMsg.AudioDirectorOperation.UpdateTrack:
            case AudioDirectorEuiMsg.AudioDirectorOperation.PauseTrack:
            case AudioDirectorEuiMsg.AudioDirectorOperation.FadeTrack:
                break;
        }
    }

    private void HandleOperationError(
        AudioDirectorEuiMsg.OperationError error)
    {
        switch (error.Operation)
        {
            case AudioDirectorEuiMsg.AudioDirectorOperation.CreateGroup:
                _createGroupWindow?.ShowError(error.Error);
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.DeleteGroup:
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.UpdateGroup:
                _editGroupWindow?.ShowError(error.Error);
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.AddTrack:
                _addTrackWindow?.ShowError(error.Error);
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.SetTrackTime:
                break;

            case AudioDirectorEuiMsg.AudioDirectorOperation.DeleteTrack:
            case AudioDirectorEuiMsg.AudioDirectorOperation.UpdateTrack:
            case AudioDirectorEuiMsg.AudioDirectorOperation.PauseTrack:
            case AudioDirectorEuiMsg.AudioDirectorOperation.FadeTrack:
                break;
        }
    }
}
