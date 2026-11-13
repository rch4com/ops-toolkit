using LdapAuthTester.Models;
using LdapAuthTester.Services;
using OpsToolkit.Shared.Models;

namespace LdapAuthTester;

public partial class MainForm
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource? _runCts;
    private LdapSettingsStore? _settingsStore;
    private bool _closing;
    private bool _resourcesDisposed;
    private int _settingsOperations;
    private readonly CheckBox _saveLocally = new() { Text = "이 PC에 서버 설정/최근 서버 저장 허용 (계정·비밀번호 제외)", AutoSize = true, Name = "saveLocally" };
    private readonly CheckBox _skipCertificateValidation = new() { Text = "현재 실행만 TLS 인증서 검증 생략 (LDAPS + LdapConnection 전용)", AutoSize = true, Name = "skipCertificateValidation" };
    private readonly NumericUpDown _timeout = new() { Minimum = 1, Maximum = 300, Value = 10, Width = 70, AccessibleName = "연결 제한 시간(초)" };
    private readonly ComboBox _presets = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, DisplayMember = "Name", AccessibleName = "저장한 서버 설정" };
    private readonly ComboBox _recentServers = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, AccessibleName = "최근 서버" };
    private readonly Button _cancel = new() { Text = "취소", Location = new Point(500, 6), Size = new Size(100, 36), Enabled = false };
    private FlowLayoutPanel _storageControls = null!;

    private void InitializeLocalSettingsControls()
    {
        const int extraHeight = 170;
        int logHeight = grpLog.Height;
        foreach (Control control in new Control[] { pnlActions, grpStatus, grpLog })
        {
            control.Top += extraHeight;
        }
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + extraHeight);
        grpLog.Height = logHeight;
        MinimumSize = new Size(900, 800);
        var group = new GroupBox { Text = "진단 옵션 / 선택적 로컬 저장", Location = new Point(12, 262), Size = new Size(860, extraHeight - 6), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8), AutoSize = true };
        var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        options.Controls.AddRange([_skipCertificateValidation, new Label { Text = "제한(초)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, _timeout]);
        rows.Controls.Add(options);
        rows.Controls.Add(_saveLocally);
        _storageControls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Enabled = false, WrapContents = false };
        _storageControls.Controls.AddRange([_presets, SettingsButton("불러오기", LoadSelectedPreset), SettingsButton("현재 설정 저장", SaveCurrentPresetAsync), SettingsButton("선택 삭제", DeleteSelectedPresetAsync)]);
        rows.Controls.Add(_storageControls);
        var history = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Enabled = false, WrapContents = false };
        history.Controls.AddRange([_recentServers, SettingsButton("서버 선택", () => { if (_recentServers.SelectedItem is string server) { txtDomain.Text = server; } return Task.CompletedTask; }), SettingsButton("최근 서버 지우기", async () => { await _settingsStore!.ClearRecentServersAsync(_lifetimeCts.Token); await RefreshSavedSettingsAsync(); })]);
        rows.Controls.Add(history);
        group.Controls.Add(rows);
        Controls.Add(group);
        pnlActions.Controls.Add(_cancel);
        _cancel.Click += (_, _) => _runCts?.Cancel();
        _saveLocally.CheckedChanged += async (_, _) =>
        {
            _storageControls.Enabled = history.Enabled = _saveLocally.Checked;
            if (!_saveLocally.Checked)
            {
                _presets.Items.Clear();
                _recentServers.Items.Clear();
                return;
            }
            _settingsStore ??= new LdapSettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LdapAuthTester"));
            await RunSettingsActionAsync(RefreshSavedSettingsAsync);
        };
        FormClosing += (_, _) => RequestShutdown();
        NativeTheme.Apply(group);
        NativeTheme.Apply(_cancel);
    }

    private Button SettingsButton(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += async (_, _) => await RunSettingsActionAsync(action);
        return button;
    }

    private async Task RunSettingsActionAsync(Func<Task> action)
    {
        if (_closing) { return; }
        _settingsOperations++;
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception ex)
        {
            if (_closing) { return; }
            MessageBox.Show(this, $"로컬 서버 설정을 처리하지 못했습니다. 파일을 확인하세요. ({ex.GetType().Name})", "로컬 저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _settingsOperations--;
            TryCleanupResources();
        }
    }

    private void RequestShutdown()
    {
        if (!_closing)
        {
            _closing = true;
            _lifetimeCts.Cancel();
            _runCts?.Cancel();
        }
        TryCleanupResources();
    }

    private void TryCleanupResources()
    {
        if (_closing && !_isRunning && _settingsOperations == 0 && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _settingsStore?.Dispose();
            _lifetimeCts.Dispose();
        }
    }

    private async Task RefreshSavedSettingsAsync()
    {
        var presets = await _settingsStore!.GetPresetsAsync(_lifetimeCts.Token);
        var history = await _settingsStore.GetRecentServersAsync(_lifetimeCts.Token);
        if (_closing) { return; }
        _presets.Items.Clear();
        _presets.Items.AddRange(presets.Cast<object>().ToArray());
        _recentServers.Items.Clear();
        _recentServers.Items.AddRange(history.Cast<object>().ToArray());
        if (_presets.Items.Count > 0) { _presets.SelectedIndex = 0; }
        if (_recentServers.Items.Count > 0) { _recentServers.SelectedIndex = 0; }
    }

    private async Task SaveCurrentPresetAsync()
    {
        var settings = new LdapServerSettings
        {
            Domain = txtDomain.Text.Trim(),
            LdapPort = (int)numLdapPort.Value,
            LdapsPort = (int)numLdapsPort.Value,
            TestMethod = (cboTestMethod.SelectedItem as ComboBoxItem<AuthTestMethod>)!.Value,
            TestTarget = (cboTestTarget.SelectedItem as ComboBoxItem<AuthTestTarget>)!.Value,
            TimeoutSeconds = (int)_timeout.Value
        };
        var existing = (await _settingsStore!.GetPresetsAsync(_lifetimeCts.Token)).FirstOrDefault(preset => string.Equals(preset.Name, settings.Domain, StringComparison.OrdinalIgnoreCase));
        await _settingsStore.SavePresetAsync(settings, existing?.Id, _lifetimeCts.Token);
        await RefreshSavedSettingsAsync();
    }

    private async Task DeleteSelectedPresetAsync()
    {
        if (_presets.SelectedItem is PresetProfile preset)
        {
            await _settingsStore!.DeletePresetAsync(preset.Id, _lifetimeCts.Token);
            await RefreshSavedSettingsAsync();
        }
    }

    private Task LoadSelectedPreset()
    {
        if (_presets.SelectedItem is PresetProfile preset)
        {
            var settings = LdapServerSettings.Deserialize(preset.SettingsJson);
            txtDomain.Text = settings.Domain;
            numLdapPort.Value = settings.LdapPort;
            numLdapsPort.Value = settings.LdapsPort;
            cboTestMethod.SelectedIndex = (int)settings.TestMethod;
            cboTestTarget.SelectedIndex = (int)settings.TestTarget;
            _timeout.Value = settings.TimeoutSeconds;
            _skipCertificateValidation.Checked = false;
        }
        return Task.CompletedTask;
    }
}
