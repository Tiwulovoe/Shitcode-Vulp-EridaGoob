// SPDX-FileCopyrightText: 2026 EridaGoob
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Erida.Lobby.UI;

/// <summary>
/// Sidebar section with a title row and a collapse arrow.
/// </summary>
public sealed class LobbySidebarPanel : BoxContainer
{
    public LobbyElement Element { get; }
    public bool Collapsed { get; private set; }

    public Control? Content => _contentHost.ChildCount > 0 ? _contentHost.GetChild(0) : null;

    private readonly BoxContainer _contentHost;
    private readonly Button _arrow;

    public LobbySidebarPanel(LobbyElement element, string title)
    {
        Element = element;
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 0;

        var header = new PanelContainer
        {
            MinHeight = 24,
        };
        EridaLobbyTheme.ApplyFloatingHeader(header);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };

        row.AddChild(new Label
        {
            Text = title,
            HorizontalExpand = true,
            ClipText = true,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = EridaLobbyTheme.TextMuted,
            StyleClasses = { "LabelSubText" },
        });

        _arrow = new Button
        {
            Text = "▼",
            MinWidth = 22,
        };
        EridaLobbyTheme.ApplyFlatButton(_arrow, Label.AlignMode.Center);
        _arrow.MinHeight = 20;
        _arrow.OnPressed += _ => ToggleCollapsed();
        row.AddChild(_arrow);

        header.AddChild(row);
        _contentHost = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 0,
        };

        AddChild(header);
        AddChild(_contentHost);

        UpdateArrow();
    }

    public void SetContent(Control content)
    {
        content.Orphan();

        while (_contentHost.ChildCount > 0)
            _contentHost.GetChild(0).Orphan();

        _contentHost.AddChild(content);
    }

    public void ToggleCollapsed()
    {
        SetCollapsed(!Collapsed);
    }

    public void SetCollapsed(bool collapsed)
    {
        Collapsed = collapsed;
        _contentHost.Visible = !collapsed;
        UpdateArrow();
    }

    private void UpdateArrow()
    {
        _arrow.Text = Collapsed ? "▶" : "▼";
        _arrow.ToolTip = Loc.GetString(Collapsed ? "lobby-sidebar-expand" : "lobby-sidebar-collapse");
    }
}
