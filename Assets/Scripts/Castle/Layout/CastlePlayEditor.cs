using UnityEngine;
using UnityEngine.InputSystem;

public enum CastleEditorTool
{
    Section = 0,
    Door = 1,
    Window = 2,
    Tower = 3,
    Heavy = 4,
    Small = 5,
    Erase = 6
}

/// <summary>Play Mode layout editor (GDD v0.5). F9 toggle, Tab target, 1-7 tools, F6 save.</summary>
public sealed class CastlePlayEditor : MonoBehaviour
{
    static readonly string[] ToolNames =
    {
        "1 Section",
        "2 Door",
        "3 Window",
        "4 Tower",
        "5 Heavy",
        "6 Small",
        "7 Erase"
    };

    [SerializeField] Camera _camera;
    [SerializeField] PlayerInputReader _input;

    bool _open;
    bool _editEnemy;
    CastleEditorTool _tool = CastleEditorTool.Section;
    int _smallKind = CastleLayoutData.SmallChest;
    CastleLayoutHost _host;
    CastlePlayEditorHud _hud;
    GameObject _ghost;

    public bool IsOpen => _open;
    public bool BlocksWeapons => _open;

    void Awake()
    {
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        _hud = CastlePlayEditorHud.Create(transform);
        _hud.SetText("Castle editor  F9", false);
    }

    void OnDestroy()
    {
        if (_ghost != null)
            Destroy(_ghost);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame)
            SetOpen(!_open);

        if (!_open)
            return;

        if (kb != null && kb.tabKey.wasPressedThisFrame)
        {
            _editEnemy = !_editEnemy;
            BindHost();
            ApplySocketVisibility();
        }

        if (kb != null && kb.f6Key.wasPressedThisFrame)
            Save();

        ReadTool(kb);
        var prevHost = _host;
        BindHost();
        if (_host != prevHost)
            ApplySocketVisibility();
        if (_host == null)
        {
            RefreshHud("No castle host in scene.");
            return;
        }

        TickGhost();

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            TryPlace();
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            TryEraseAimed();
    }

    void ReadTool(Keyboard kb)
    {
        if (kb == null)
            return;
        if (kb.digit1Key.wasPressedThisFrame) _tool = CastleEditorTool.Section;
        else if (kb.digit2Key.wasPressedThisFrame) _tool = CastleEditorTool.Door;
        else if (kb.digit3Key.wasPressedThisFrame) _tool = CastleEditorTool.Window;
        else if (kb.digit4Key.wasPressedThisFrame) _tool = CastleEditorTool.Tower;
        else if (kb.digit5Key.wasPressedThisFrame) _tool = CastleEditorTool.Heavy;
        else if (kb.digit6Key.wasPressedThisFrame) _tool = CastleEditorTool.Small;
        else if (kb.digit7Key.wasPressedThisFrame) _tool = CastleEditorTool.Erase;

        if (_tool == CastleEditorTool.Small && kb.rKey.wasPressedThisFrame)
        {
            _smallKind++;
            if (_smallKind > CastleLayoutData.SmallPlanter)
                _smallKind = CastleLayoutData.SmallChest;
        }
    }

    void SetOpen(bool on)
    {
        _open = on;
        if (_ghost != null)
            _ghost.SetActive(on);

        BindHost();
        ApplySocketVisibility();
        if (!on)
            RefreshHud("Castle editor  F9");
    }

    void BindHost()
    {
        _host = _editEnemy ? CastleLayoutHost.FindEnemy() : CastleLayoutHost.FindPlayer();
    }

    void ApplySocketVisibility()
    {
        var player = CastleLayoutHost.FindPlayer();
        var enemy = CastleLayoutHost.FindEnemy();
        if (player != null)
            player.SetSocketsVisible(_open && player == _host);
        if (enemy != null)
            enemy.SetSocketsVisible(_open && enemy == _host);
    }

    void Save()
    {
        if (_host == null)
            return;
        _host.Save();
        RefreshHud("Saved " + _host.SaveId);
    }

    void TryPlace()
    {
        if (_tool == CastleEditorTool.Erase)
        {
            TryEraseAimed();
            return;
        }

        if (!Raycast(out RaycastHit hit))
            return;

        var sock = hit.collider.GetComponentInParent<CastleEditorSocket>();
        var data = _host.Data;

        if (_tool == CastleEditorTool.Section)
        {
            CellFromHit(hit, out int x, out int y, out int z);
            if (data.TryAddSection(x, y, z))
                _host.Rebuild();
            return;
        }

        if (sock == null)
            return;

        if (_tool == CastleEditorTool.Door || _tool == CastleEditorTool.Window)
        {
            if (sock.SocketKind != CastleEditorSocket.Kind.Opening)
                return;
            if (_tool == CastleEditorTool.Door && sock.Row != 0)
                return;
            int kind = _tool == CastleEditorTool.Door
                ? CastleLayoutData.OpeningDoor
                : CastleLayoutData.OpeningWindow;
            data.SetOpening(sock.X, sock.Y, sock.Z, sock.Wall, sock.Col, sock.Row, kind);
            _host.Rebuild();
            return;
        }

        if (_tool == CastleEditorTool.Tower || _tool == CastleEditorTool.Heavy)
        {
            if (sock.SocketKind != CastleEditorSocket.Kind.Power)
                return;
            if (!data.IsOuterPowerCorner(sock.X, sock.Z, (CastleBuildMetrics.CornerId)sock.Corner))
                return;
            int kind = _tool == CastleEditorTool.Tower
                ? CastleLayoutData.PowerTower
                : CastleLayoutData.PowerHeavy;
            data.SetPower(sock.X, sock.Z, sock.Corner, kind);
            _host.Rebuild();
            return;
        }

        if (_tool == CastleEditorTool.Small)
        {
            if (sock.SocketKind != CastleEditorSocket.Kind.Small)
                return;
            data.SetSmall(sock.X, sock.Y, sock.Z, sock.Index, _smallKind);
            _host.Rebuild();
        }
    }

    void TryEraseAimed()
    {
        if (!Raycast(out RaycastHit hit))
            return;

        var sock = hit.collider.GetComponentInParent<CastleEditorSocket>();
        if (sock == null)
            return;

        var data = _host.Data;
        bool changed = false;
        if (sock.SocketKind == CastleEditorSocket.Kind.Opening)
        {
            data.SetOpening(sock.X, sock.Y, sock.Z, sock.Wall, sock.Col, sock.Row, 0);
            changed = true;
        }
        else if (sock.SocketKind == CastleEditorSocket.Kind.Small)
        {
            data.SetSmall(sock.X, sock.Y, sock.Z, sock.Index, 0);
            changed = true;
        }
        else if (sock.SocketKind == CastleEditorSocket.Kind.Power)
        {
            data.SetPower(sock.X, sock.Z, sock.Corner, 0);
            changed = true;
        }
        else if (sock.SocketKind == CastleEditorSocket.Kind.Section)
        {
            if (data.sections.Count > 1)
                changed = data.RemoveSection(sock.X, sock.Y, sock.Z);
        }

        if (changed)
            _host.Rebuild();
    }

    void CellFromHit(RaycastHit hit, out int x, out int y, out int z)
    {
        Vector3 local = _host.transform.InverseTransformPoint(hit.point + hit.normal * 0.55f);
        CastleBuildMetrics.WorldToCell(local, out x, out y, out z);
    }

    bool Raycast(out RaycastHit hit)
    {
        hit = default;
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        if (_camera == null)
            return false;
        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
        return Physics.Raycast(ray, out hit, 48f, ~0, QueryTriggerInteraction.Collide);
    }

    void TickGhost()
    {
        if (_ghost == null)
        {
            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "CastleEditorGhost";
            _ghost.layer = 2;
            Object.Destroy(_ghost.GetComponent<Collider>());
            CastlePrim.Tint(_ghost, new Color(0.3f, 0.9f, 1f, 0.35f));
        }

        _ghost.SetActive(true);
        if (!Raycast(out RaycastHit hit))
        {
            _ghost.SetActive(false);
            RefreshHud(StatusLine("no hit"));
            return;
        }

        var sock = hit.collider.GetComponentInParent<CastleEditorSocket>();
        _ghost.transform.position = hit.point;
        _ghost.transform.localScale = Vector3.one * 0.45f;
        RefreshHud(StatusLine(sock != null ? sock.SocketKind.ToString() : hit.collider.name));
    }

    string StatusLine(string aim)
    {
        string target = _editEnemy ? "ENEMY" : "PLAYER";
        string small = _smallKind == CastleLayoutData.SmallChest ? "chest"
            : _smallKind == CastleLayoutData.SmallWorkbench ? "bench" : "planter";
        string tool = ToolNames[(int)_tool];
        if (_tool == CastleEditorTool.Small)
            tool += " (" + small + ", R cycle)";
        return $"CASTLE EDITOR  [{target}]\n{tool}\nAim: {aim}\nTab castle   F6 save   F9 close\nLMB place   RMB erase";
    }

    void RefreshHud(string text)
    {
        if (_hud == null)
            _hud = CastlePlayEditorHud.Create(transform);
        _hud.SetText(text, _open);
    }
}
