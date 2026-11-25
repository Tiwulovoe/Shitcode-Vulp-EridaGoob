// SPDX-FileCopyrightText: 2026 EridaGoob
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Erida.Lobby.UI;

/// <summary>
/// BlueMoon-inspired lobby colours and style boxes.
/// Robust UI has no CSS, so the flat blue panels and hover-highlighted menu rows are built directly.
/// </summary>
public static class EridaLobbyTheme
{
    public static readonly Color PanelBackground = new(8, 16, 42, 225);
    public static readonly Color PanelBackgroundStrong = new(4, 9, 26, 242);
    public static readonly Color PanelBorder = new(52, 104, 178, 150);
    public static readonly Color PanelBorderBright = new(77, 166, 255, 220);
    public static readonly Color HoverBackground = new(24, 56, 112, 210);
    public static readonly Color AccentBackground = new(24, 76, 140, 235);
    public static readonly Color AccentBackgroundHover = new(35, 96, 168, 245);
    public static readonly Color DangerBackground = new(120, 24, 38, 220);
    public static readonly Color Text = new(204, 227, 255);
    public static readonly Color TextMuted = new(111, 143, 191);
    public static readonly Color TextDim = new(70, 96, 136);
    public static readonly Color Accent = new(120, 190, 255);
    public static readonly Color Success = new(76, 255, 158);
    public static readonly Color Danger = new(255, 112, 128);

    public static readonly Color Overlay = new(0, 4, 18, 150);

    public static StyleBoxFlat Box(Color background, Color border, float borderThickness = 1f,
        float contentLeft = 0f, float contentTop = 0f, float contentRight = 0f, float contentBottom = 0f)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(borderThickness),
            ContentMarginLeftOverride = contentLeft,
            ContentMarginTopOverride = contentTop,
            ContentMarginRightOverride = contentRight,
            ContentMarginBottomOverride = contentBottom,
        };
    }

    public static void ApplyOverlay(PanelContainer panel)
    {
        panel.PanelOverride = Box(Overlay, Color.Transparent, 0f);
    }

    public static void ApplySidebar(PanelContainer panel)
    {
        panel.PanelOverride = Box(PanelBackgroundStrong, PanelBorder, 1f, 0f, 0f, 0f, 0f);
    }

    public static void ApplySection(PanelContainer panel)
    {
        panel.PanelOverride = Box(PanelBackground, PanelBorder, 1f, 8f, 8f, 8f, 8f);
    }

    public static void ApplyNotice(PanelContainer panel)
    {
        panel.PanelOverride = Box(new Color(6, 12, 34, 240), PanelBorderBright, 1f, 12f, 8f, 12f, 8f);
    }

    public static void ApplyFloatingHeader(PanelContainer panel)
    {
        panel.PanelOverride = Box(new Color(10, 22, 52, 245), PanelBorderBright, 1f, 6f, 2f, 6f, 2f);
    }

    public static void ApplyDivider(PanelContainer panel)
    {
        panel.PanelOverride = Box(new Color(77, 166, 255, 70), Color.Transparent, 0f);
    }

    public static void ApplyInput(OptionButton option)
    {
        option.StyleBoxOverride = Box(new Color(5, 12, 30, 230), PanelBorder, 1f, 8f, 4f, 8f, 4f);
    }

    private static readonly StyleBox SelectableNormal =
        Box(Color.Transparent, Color.Transparent, 0f, 4f, 2f, 4f, 2f);

    private static readonly StyleBox SelectableHover =
        Box(HoverBackground, new Color(77, 166, 255, 90), 1f, 4f, 2f, 4f, 2f);

    private static readonly StyleBox SelectablePressed =
        Box(AccentBackground, PanelBorderBright, 1f, 4f, 2f, 4f, 2f);

    /// <summary>
    /// Styles a toggle button (layout presets) so the selected preset is clearly highlighted.
    /// </summary>
    public static void ApplySelectableButton(Button button, Label.AlignMode align = Label.AlignMode.Center)
    {
        button.ToggleMode = true;
        button.TextAlign = align;
        button.MinHeight = 26;
        button.StyleBoxOverride = SelectableNormal;
        button.Label.FontColorOverride = Text;

        button.OnMouseEntered += _ => UpdateSelectableButton(button, true);
        button.OnMouseExited += _ => UpdateSelectableButton(button, false);
        button.OnToggled += _ => UpdateSelectableButton(button, false);
    }

    /// <summary>
    /// Refreshes a selectable button after <see cref="BaseButton.Pressed"/> was changed in code.
    /// </summary>
    public static void RefreshSelectableButton(Button button)
    {
        UpdateSelectableButton(button, false);
    }

    private static void UpdateSelectableButton(Button button, bool hovered)
    {
        button.StyleBoxOverride = button.Pressed
            ? SelectablePressed
            : hovered ? SelectableHover : SelectableNormal;

        button.Label.FontColorOverride = button.Pressed
            ? Color.White
            : hovered ? Accent : Text;
    }

    public static void ApplySlider(Slider slider)
    {
        slider.BackgroundStyleBoxOverride = Box(new Color(8, 20, 46, 255), PanelBorder, 1f);
        slider.ForegroundStyleBoxOverride = Box(Color.Transparent, Color.Transparent, 0f);
        slider.FillStyleBoxOverride = Box(new Color(45, 120, 200, 255), PanelBorderBright, 0f);
        slider.GrabberStyleBoxOverride = Box(Accent, PanelBorderBright, 1f, 4f, 8f, 4f, 8f);
    }

    public static void ApplyFlatButton(Button button, Label.AlignMode align = Label.AlignMode.Left)
    {
        ApplyButton(button, Box(Color.Transparent, Color.Transparent, 0f, 4f, 2f, 4f, 2f),
            Box(HoverBackground, new Color(77, 166, 255, 90), 1f, 4f, 2f, 4f, 2f),
            Text, Accent, align);
    }

    public static void ApplyAccentButton(Button button, Label.AlignMode align = Label.AlignMode.Center)
    {
        ApplyButton(button, Box(AccentBackground, PanelBorderBright, 1f, 8f, 4f, 8f, 4f),
            Box(AccentBackgroundHover, Accent, 1f, 8f, 4f, 8f, 4f),
            Color.White, Color.White, align);
    }

    public static void ApplyDangerButton(Button button, Label.AlignMode align = Label.AlignMode.Left)
    {
        ApplyButton(button, Box(Color.Transparent, Color.Transparent, 0f, 4f, 2f, 4f, 2f),
            Box(DangerBackground, Danger, 1f, 4f, 2f, 4f, 2f),
            Danger, Color.White, align);
    }

    private static void ApplyButton(Button button, StyleBox normal, StyleBox hover, Color normalColor,
        Color hoverColor, Label.AlignMode align)
    {
        button.TextAlign = align;
        button.MinHeight = 26;
        button.StyleBoxOverride = normal;
        button.Label.FontColorOverride = normalColor;

        button.OnMouseEntered += _ =>
        {
            if (button.Disabled)
                return;

            button.StyleBoxOverride = hover;
            button.Label.FontColorOverride = hoverColor;
        };

        button.OnMouseExited += _ =>
        {
            button.StyleBoxOverride = normal;
            button.Label.FontColorOverride = normalColor;
        };
    }

    /// <summary>
    /// Styles every plain button under <paramref name="root"/>. Used for the link banner and the
    /// character preview, whose buttons are created outside of XAML.
    /// </summary>
    public static void ApplyButtonsRecursive(Control root, Label.AlignMode align = Label.AlignMode.Left)
    {
        foreach (var child in root.Children)
        {
            if (child is Button button)
                ApplyFlatButton(button, align);

            ApplyButtonsRecursive(child, align);
        }
    }
}
