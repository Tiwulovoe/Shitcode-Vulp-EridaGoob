// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Erida.Lobby;
using Content.Client._RMC14.LinkAccount;
using Content.Client.Audio;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Goobstation.Common.ServerCurrency;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Client._Impstation.ReadyManifest; // Impstation - Ready Manifest

namespace Content.Client.Lobby
{
    public sealed partial class LobbyState : Robust.Client.State.State // Erida edit
    {
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IVoteManager _voteManager = default!;
        [Dependency] private readonly ICommonCurrencyManager _serverCur = default!; // Goobstation - server currency
        [Dependency] private readonly IPrototypeManager _protoMan = default!; // Goobstation - credits
        [Dependency] private readonly LinkAccountManager _linkAccount = default!; // RMC - Patreon
        [Dependency] private readonly ClientsidePlaytimeTrackingManager _playtimeTracking = default!;

        private ISawmill _sawmill = default!; // Goobstation
        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;
        private ReadyManifestSystem _readyManifest = default!; // Impstation - Ready Manifest
        private bool _updatingLobbyMusicUi;
        private float _lobbyVolumeBeforeMute = 0.5f;

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            _contentAudioSystem = _entityManager.System<ContentAudioSystem>();
            _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;
            _sawmill = Logger.GetSawmill("lobby");
            _readyManifest = _entityManager.EntitySysManager.GetEntitySystem<ReadyManifestSystem>(); // Impstation - Ready Manifest

            chatController.SetMainChat(true);

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
            var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

            Lobby.ServerName.Text = string.IsNullOrEmpty(lobbyNameCvar)
                ? Loc.GetString("ui-lobby-title", ("serverName", serverName))
                : lobbyNameCvar;

            var width = _cfg.GetCVar(CCVars.ServerLobbyRightPanelWidth);
            Lobby.RightSide.SetWidth = Math.Clamp(width, 300, 420);

            UpdateLobbyUi();

            Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.CharacterPreview.PatronPerks.OnPressed += OnPatronPerksPressed;
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;
            Lobby.ManifestButton.OnPressed += OnManifestPressed; // Impstation - Ready Manifest

            Lobby.LobbyMusicPlayButton.OnPressed += OnLobbyMusicPlayPressed;
            Lobby.LobbyMusicPrevButton.OnPressed += OnLobbyMusicPrevPressed;
            Lobby.LobbyMusicNextButton.OnPressed += OnLobbyMusicNextPressed;
            Lobby.LobbyMusicMuteButton.OnPressed += OnLobbyMusicMutePressed;
            Lobby.LobbyMusicVolume.OnValueChanged += OnLobbyMusicVolumeChanged;
            Lobby.LobbyMusicEnabledCheck.OnToggled += OnLobbyMusicEnabledToggled;
            _contentAudioSystem.LobbyMusicStateChanged += UpdateLobbyMusicPlayer;
            UpdateLobbyMusicPlayer();
            _contentAudioSystem.EnsureLobbyMusicPlaying();

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;

            _serverCur.ClientBalanceChange += UpdatePlayerBalance; // Goobstation - Goob Coin
            StartupErida(); // Erida edit
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;
            _serverCur.ClientBalanceChange -= UpdatePlayerBalance; // Goobstation - Goob Coin
            ShutdownErida(); // Erida edit

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterPreview.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby.CharacterPreview.PatronPerks.OnPressed -= OnPatronPerksPressed;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;
            Lobby!.ManifestButton.OnPressed -= OnManifestPressed; // Impstation - Ready Manifest

            Lobby.LobbyMusicPlayButton.OnPressed -= OnLobbyMusicPlayPressed;
            Lobby.LobbyMusicPrevButton.OnPressed -= OnLobbyMusicPrevPressed;
            Lobby.LobbyMusicNextButton.OnPressed -= OnLobbyMusicNextPressed;
            Lobby.LobbyMusicMuteButton.OnPressed -= OnLobbyMusicMutePressed;
            Lobby.LobbyMusicVolume.OnValueChanged -= OnLobbyMusicVolumeChanged;
            Lobby.LobbyMusicEnabledCheck.OnToggled -= OnLobbyMusicEnabledToggled;
            _contentAudioSystem.LobbyMusicStateChanged -= UpdateLobbyMusicPlayer;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            SetReady(false);
            Lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnPatronPerksPressed(BaseButton.ButtonEventArgs obj)
        {
            _userInterfaceManager.GetUIController<LinkAccountUIController>().TogglePatronPerksWindow();
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
            {
                return;
            }

            new LateJoinGui().OpenCentered();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        // Impstation - Ready Manifest
        private void OnManifestPressed(BaseButton.ButtonEventArgs args)
        {
            _readyManifest.RequestReadyManifest();
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            UpdateEridaLobbyBackground(e.DeltaSeconds); // Erida edit

            // Keep lobby music playing by default; resumes a paused track or starts one as soon
            // as the server playlist is available, regardless of state-change event ordering.
            _contentAudioSystem.EnsureLobbyMusicPlaying();

            if (_gameTicker.IsGameStarted)
            {
                Lobby!.StartTime.Text = string.Empty;
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                return;
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
            {
                text = Loc.GetString("lobby-state-paused");
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyBackground();
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
        }

        private void UpdateLobbyUi()
        {
            Lobby!.CharacterPreview.PatronPerks.Visible = _linkAccount.CanViewPatronPerks();

            if (_gameTicker.IsGameStarted)
            {
                Lobby!.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
                Lobby!.ReadyButton.ToggleMode = false;
                Lobby!.ReadyButton.Pressed = false;
                Lobby!.ObserveButton.Disabled = false;
                Lobby!.ManifestButton.Disabled = true; //imp
            }
            else
            {
                Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed ? "lobby-state-player-status-ready" : "lobby-state-player-status-not-ready");
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ObserveButton.Disabled = true;
                Lobby!.ManifestButton.Disabled = false; // imp
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
            }

            UpdatePlayerBalance(); // Goobstation - Goob Coin

            var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
            if (minutesToday > 60)
            {
                Lobby!.PlaytimeComment.Visible = true;

                var hoursToday = Math.Round(minutesToday / 60f, 1);

                var chosenString = minutesToday switch
                {
                    < 180 => "lobby-state-playtime-comment-normal",
                    < 360 => "lobby-state-playtime-comment-concerning",
                    < 720 => "lobby-state-playtime-comment-grasstouchless",
                    _ => "lobby-state-playtime-comment-selfdestructive"
                };

                Lobby.PlaytimeComment.SetMarkup(Loc.GetString(chosenString, ("hours", hoursToday)));
            }
            else
                Lobby!.PlaytimeComment.Visible = false;
        }

        private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
        {
            if (ev.SoundtrackFilename == null)
            {
                Lobby!.LobbySong.SetMarkup(Loc.GetString("lobby-state-song-no-song-text"));
            }
            else if (
                ev.SoundtrackFilename != null
                && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
                )
            {
                var lobbyStream = lobbySongResource.AudioStream;

                var title = string.IsNullOrEmpty(lobbyStream.Title)
                    ? Loc.GetString("lobby-state-song-unknown-title")
                    : lobbyStream.Title;

                var artist = string.IsNullOrEmpty(lobbyStream.Artist)
                    ? Loc.GetString("lobby-state-song-unknown-artist")
                    : lobbyStream.Artist;

                var markup = Loc.GetString("lobby-state-song-text",
                    ("songTitle", title),
                    ("songArtist", artist));

                Lobby!.LobbySong.SetMarkup(markup);
            }
        }

        // Goobstation - heavily modified to add credits for lobby backgrounds
        private void UpdateLobbyBackground()
        {
            // Erida start
            if (GetEridaLobbyBackground() is { } backgroundId)
            {
                var lobbyBackground = _protoMan.Index(backgroundId);
                SetEridaLobbyBackground(lobbyBackground);
                var name = EridaLobbyBackgrounds.Localize(lobbyBackground.Name, "lobby-state-background-unknown-title");
                var artist = EridaLobbyBackgrounds.Localize(lobbyBackground.Artist, "lobby-state-background-unknown-artist");
                // Erida end

                var markup = Loc.GetString("lobby-state-background-text",
                    ("backgroundName", name),
                    ("backgroundArtist", artist));

                Lobby!.LobbyBackground.SetMarkup(markup);

                return;
            }

            _sawmill.Warning("_gameTicker.LobbyBackground was null! No lobby background selected.");
            SetEridaLobbyBackground(null); // Erida edit
            Lobby!.LobbyBackground.SetMarkup(Loc.GetString("lobby-state-background-no-background-text"));
        }

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }

        private void UpdatePlayerBalance() // Goobstation - Goob Coin
        {
            Lobby!.Balance.Text = _serverCur.Stringify(_serverCur.GetBalance());
        }

        private void OnLobbyMusicPlayPressed(BaseButton.ButtonEventArgs args)
        {
            _contentAudioSystem.ToggleLobbyMusicPaused();
        }

        private void OnLobbyMusicPrevPressed(BaseButton.ButtonEventArgs args)
        {
            _contentAudioSystem.PlayPreviousLobbyTrack();
        }

        private void OnLobbyMusicNextPressed(BaseButton.ButtonEventArgs args)
        {
            _contentAudioSystem.PlayNextLobbyTrack();
        }

        private void OnLobbyMusicMutePressed(BaseButton.ButtonEventArgs args)
        {
            var volume = _contentAudioSystem.GetLobbyMusicVolume();

            if (volume > 0.001f)
            {
                _lobbyVolumeBeforeMute = volume;
                _contentAudioSystem.SetLobbyMusicVolume(0f);
            }
            else
            {
                _contentAudioSystem.SetLobbyMusicVolume(_lobbyVolumeBeforeMute <= 0.001f ? 0.5f : _lobbyVolumeBeforeMute);
            }
        }

        private void OnLobbyMusicVolumeChanged(Robust.Client.UserInterface.Controls.Range range)
        {
            if (_updatingLobbyMusicUi)
                return;

            var volume = Math.Clamp(range.Value / 100f, 0f, 1f);
            if (volume > 0.001f)
                _lobbyVolumeBeforeMute = volume;

            _contentAudioSystem.SetLobbyMusicVolume(volume);
        }

        private void OnLobbyMusicEnabledToggled(BaseButton.ButtonToggledEventArgs args)
        {
            if (_updatingLobbyMusicUi)
                return;

            _contentAudioSystem.SetLobbyMusicEnabled(args.Pressed);
        }

        /// <summary>
        /// Enables lobby music once for players whose archived settings were saved before the
        /// in-lobby player existed, and restores a zero (muted) volume to a sensible default.
        /// Later explicit changes to the checkbox/volume are respected.
        /// </summary>
        private void UpdateLobbyMusicPlayer()
        {
            if (Lobby == null)
                return;

            _updatingLobbyMusicUi = true;

            var enabled = _contentAudioSystem.LobbyMusicEnabled;
            var paused = _contentAudioSystem.LobbyMusicPaused;
            var volume = _contentAudioSystem.GetLobbyMusicVolume();
            var hasTrack = _contentAudioSystem.CurrentLobbySoundtrack != null;

            Lobby.LobbyMusicEnabledCheck.Pressed = enabled;
            Lobby.LobbyMusicPlayButton.Text = paused || !hasTrack ? "▶" : "II";
            Lobby.LobbyMusicPlayButton.ToolTip = Loc.GetString(paused || !hasTrack ? "lobby-music-play" : "lobby-music-pause");
            Lobby.LobbyMusicPlayButton.Disabled = !enabled || !hasTrack;
            Lobby.LobbyMusicPrevButton.Disabled = !enabled || _contentAudioSystem.LobbyPlaylistLength <= 1;
            Lobby.LobbyMusicNextButton.Disabled = !enabled || _contentAudioSystem.LobbyPlaylistLength <= 1;
            Lobby.LobbyMusicMuteButton.Text = volume <= 0.001f ? "♪×" : "♪";
            Lobby.LobbyMusicMuteButton.ToolTip = Loc.GetString(volume <= 0.001f ? "lobby-music-unmute" : "lobby-music-mute");
            Lobby.LobbyMusicMuteButton.Disabled = !enabled;
            Lobby.LobbyMusicVolume.Disabled = !enabled;
            Lobby.LobbyMusicVolume.SetValueWithoutEvent(volume * 100f);

            _updatingLobbyMusicUi = false;
        }
    }
}
