// SPDX-FileCopyrightText: 2026 EridaGoob
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client._Erida.Lobby.UI;

/// <summary>
/// A separate on-screen lobby panel. The header is a drag handle, and the panel collides with
/// the screen bounds and with the other floating panels.
/// </summary>
public sealed class LobbyFloatingPanel : BoxContainer
{
    public LobbyElement Element { get; }
    public LobbyScreenDock Dock { get; }

    /// <summary>
    /// True once the player dragged this panel. Such panels keep their user position across
    /// layout changes and sidebar toggles instead of being re-docked.
    /// </summary>
    public bool HasBeenMoved { get; private set; }

    public Vector2? UserPosition { get; private set; }

    public Control? Content => _contentHost.ChildCount > 0 ? _contentHost.GetChild(0) : null;

    private readonly BoxContainer _contentHost;

    public LobbyFloatingPanel(LobbyElement element, LobbyScreenDock dock, string title)
    {
        Element = element;
        Dock = dock;
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 0;

        _contentHost = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 0,
        };

        AddChild(new LobbyDragHandle(this, title));
        AddChild(_contentHost);
    }

    public void SetContent(Control content)
    {
        content.Orphan();

        while (_contentHost.ChildCount > 0)
            _contentHost.GetChild(0).Orphan();

        _contentHost.AddChild(content);
    }

    public void RemoveContent()
    {
        while (_contentHost.ChildCount > 0)
            _contentHost.GetChild(0).Orphan();
    }

    /// <summary>
    /// Positions the panel without marking it as manually moved.
    /// </summary>
    public void SetInitialPosition(Vector2 position)
    {
        LayoutContainer.SetPosition(this, position);
    }

    /// <summary>
    /// Forgets a manual position and lets the panel dock to its default corner again.
    /// </summary>
    public void ResetUserPosition()
    {
        HasBeenMoved = false;
        UserPosition = null;
    }

    /// <summary>
    /// Moves the panel, clamping it to the layer and resolving collisions with the other panels.
    /// </summary>
    public bool TryMoveTo(Vector2 target)
    {
        if (Parent is not Control layer)
            return false;

        var size = Size;
        if (size.X <= 0 || size.Y <= 0)
            size = DesiredSize;
        if (size.X <= 0 || size.Y <= 0)
            return false;

        target = ClampPosition(target, layer.Size, size);

        if (TryPlace(target, size))
            return true;

        // Allow sliding along the other panel instead of freezing completely.
        if (TryPlace(ClampPosition(new Vector2(target.X, Position.Y), layer.Size, size), size))
            return true;

        if (TryPlace(ClampPosition(new Vector2(Position.X, target.Y), layer.Size, size), size))
            return true;

        return false;
    }

    private bool TryPlace(Vector2 position, Vector2 size)
    {
        if (Collides(position, size))
            return false;

        LayoutContainer.SetPosition(this, position);
        HasBeenMoved = true;
        UserPosition = position;
        return true;
    }

    private bool Collides(Vector2 position, Vector2 size)
    {
        if (Parent is not Control layer)
            return false;

        var rect = new UIBox2(position, position + size);
        foreach (var child in layer.Children)
        {
            if (child == this || child is not LobbyFloatingPanel other || !other.Visible)
                continue;

            var otherSize = other.Size;
            if (otherSize.X <= 0 || otherSize.Y <= 0)
                otherSize = other.DesiredSize;
            if (otherSize.X <= 0 || otherSize.Y <= 0)
                continue;

            var otherRect = new UIBox2(other.Position, other.Position + otherSize);
            if (rect.Intersects(otherRect))
                return true;
        }

        return false;
    }

    public static Vector2 ClampPosition(Vector2 position, Vector2 layerSize, Vector2 size)
    {
        var maxX = MathF.Max(0f, layerSize.X - size.X);
        var maxY = MathF.Max(0f, layerSize.Y - size.Y);
        return new Vector2(Math.Clamp(position.X, 0f, maxX), Math.Clamp(position.Y, 0f, maxY));
    }

    public sealed class LobbyDragHandle : PanelContainer
    {
        private readonly LobbyFloatingPanel _panel;
        private bool _dragging;
        private Vector2 _offset;

        public LobbyDragHandle(LobbyFloatingPanel panel, string title)
        {
            _panel = panel;
            MouseFilter = MouseFilterMode.Stop;
            DefaultCursorShape = CursorShape.Move;
            MinHeight = 22;
            ToolTip = Loc.GetString("lobby-panel-drag-tooltip");
            EridaLobbyTheme.ApplyFloatingHeader(this);

            AddChild(new Label
            {
                Text = $"≡  {title}",
                MouseFilter = MouseFilterMode.Ignore,
                FontColorOverride = EridaLobbyTheme.TextMuted,
                StyleClasses = { "LabelSubText" },
                VerticalAlignment = VAlignment.Center,
            });
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);

            if (args.Function != EngineKeyFunctions.UIClick || _panel.Parent is not Control layer)
                return;

            _dragging = true;
            var pointer = args.PointerLocation.Position / UIScale;
            _offset = pointer - layer.GlobalPosition - _panel.Position;
            _panel.SetPositionLast();
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);

            if (!_dragging || _panel.Parent is not Control layer)
                return;

            var pointer = args.GlobalPosition - layer.GlobalPosition;
            _panel.TryMoveTo(pointer - _offset);
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);

            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            _dragging = false;
            UserInterfaceManager.KeyboardFocused?.ReleaseKeyboardFocus();
        }
    }
}
