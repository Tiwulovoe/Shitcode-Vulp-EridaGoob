// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Changelog;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Client.Console;
using Content.Client._Erida.Discord.Linking;

namespace Content.Client.Info
{
    public sealed class LinkBanner : BoxContainer
    {
        private readonly IConfigurationManager _cfg;
        private readonly BoxContainer _buttons;

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        /// <summary>
        /// Direction of the link buttons. The lobby sidebar uses a vertical list that fills the
        /// sidebar width so the button captions can be centered.
        /// </summary>
        public new LayoutOrientation Orientation
        {
            get => base.Orientation;
            set
            {
                base.Orientation = value;
                _buttons.Orientation = value;
            }
        }

        public LinkBanner()
        {
            _buttons = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 2,
            };
            AddChild(_buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();
            _cfg = IoCManager.Resolve<IConfigurationManager>();

            var rulesButton = new Button() { Text = Loc.GetString("server-info-rules-button") };
            rulesButton.OnPressed += args => new RulesAndInfoWindow().Open();
            _buttons.AddChild(rulesButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);
            AddInfoButton("rmc-ui-patreon", CCVars.InfoLinksPatreon);

            var linkAccount = UserInterfaceManager.GetUIController<DiscordLinkingUIController>();
            var linkAccountButton = new Button
            {
                Text = Loc.GetString("rmc-ui-link-discord-account"),
            };
            linkAccountButton.OnPressed += _ => linkAccount.ToggleWindow();
            _buttons.AddChild(linkAccountButton);

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            var guidebookButton = new Button() { Text = Loc.GetString("server-info-guidebook-button") };
            guidebookButton.OnPressed += _ =>
            {
                guidebookController.ToggleGuidebook();
            };
            _buttons.AddChild(guidebookButton);

            // Erida start
            var pollsButton = new Button() { Text = Loc.GetString("server-info-polls-button") };
            pollsButton.OnPressed += _ =>
            {
                IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand("polls");
            };
            _buttons.AddChild(pollsButton);
            // Erida end

            var changelogButton = new ChangelogButton();
            changelogButton.OnPressed += args => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
            _buttons.AddChild(changelogButton);

            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = new Button { Text = Loc.GetString(loc) };
                button.OnPressed += _ => uriOpener.OpenUri(_cfg.GetCVar(cVar));
                _buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }
        }
    }
}
