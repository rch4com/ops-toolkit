using LdapAuthTester.Models;
using LdapAuthTester.Services;

namespace LdapAuthTester;

public partial class MainForm : Form
{
    private readonly AdAuthChecker _authChecker;
    private bool _isRunning;

    public MainForm()
    {
        InitializeComponent();
        _authChecker = new AdAuthChecker();
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        InitComboBoxes();
        LoadEnvironmentInfo();
    }

    private void InitComboBoxes()
    {
        cboTestTarget.Items.Clear();
        cboTestTarget.Items.Add(new ComboBoxItem<AuthTestTarget>("LDAP & LDAPS 둘 다 테스트 (순차 실행)", AuthTestTarget.Both));
        cboTestTarget.Items.Add(new ComboBoxItem<AuthTestTarget>("LDAP (389 / 일반) 단독 테스트", AuthTestTarget.LdapOnly));
        cboTestTarget.Items.Add(new ComboBoxItem<AuthTestTarget>("LDAPS (636 / SSL) 단독 테스트", AuthTestTarget.LdapsOnly));
        cboTestTarget.SelectedIndex = 0;

        cboTestMethod.Items.Clear();
        cboTestMethod.Items.Add(new ComboBoxItem<AuthTestMethod>("DirectoryEntry (NativeObject 바인딩 - 권장)", AuthTestMethod.DirectoryEntry));
        cboTestMethod.Items.Add(new ComboBoxItem<AuthTestMethod>("PrincipalContext (ValidateCredentials)", AuthTestMethod.PrincipalContext));
        cboTestMethod.Items.Add(new ComboBoxItem<AuthTestMethod>("LdapConnection (System.DirectoryServices.Protocols)", AuthTestMethod.LdapConnection));
        cboTestMethod.SelectedIndex = 0;
    }

    private void LoadEnvironmentInfo()
    {
        try
        {
            AdEnvironmentInfo env = _authChecker.GetCurrentEnvironmentInfo();

            if (env.IsDomainJoined)
            {
                lblDomainJoinStatus.Text = $"✔ AD 도메인 조인됨: {env.DomainName}";
                lblDomainJoinStatus.ForeColor = Color.FromArgb(0, 153, 76);
            }
            else
            {
                lblDomainJoinStatus.Text = $"⚠ 도메인 미조인 또는 감지 불가 ({env.DetectionMessage})";
                lblDomainJoinStatus.ForeColor = Color.FromArgb(204, 102, 0);
            }

            string dcText = string.IsNullOrWhiteSpace(env.DomainController) ? "-" : env.DomainController;
            lblEnvDetails.Text = $"로그온 사용자: {env.CurrentLogonUser} | 감지 DC: {dcText}";

            if (string.IsNullOrWhiteSpace(txtDomain.Text) && !string.IsNullOrWhiteSpace(env.DomainName))
            {
                txtDomain.Text = env.DomainName;
            }

            if (string.IsNullOrWhiteSpace(txtUserName.Text))
            {
                txtUserName.Text = Environment.UserName;
            }

            AppendLog($"[환경 정보 로드 완료] 도메인: '{env.DomainName}', DC: '{dcText}', 현재 사용자: '{env.CurrentLogonUser}'", Color.FromArgb(100, 180, 255));
        }
        catch (Exception ex)
        {
            lblDomainJoinStatus.Text = "환경 정보 조회 실패";
            lblDomainJoinStatus.ForeColor = Color.Red;
            lblEnvDetails.Text = ex.Message;
            AppendLog($"[환경 정보 로드 오류] {ex}", Color.Salmon);
        }
    }

    private void BtnReloadEnv_Click(object? sender, EventArgs e)
    {
        LoadEnvironmentInfo();
    }

    private void ChkShowPassword_CheckedChanged(object? sender, EventArgs e)
    {
        txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;
    }

    private async void BtnRunTest_Click(object? sender, EventArgs e)
    {
        await ExecuteTestFlowAsync(isDryRun: false).ConfigureAwait(true);
    }

    private async void BtnDryRun_Click(object? sender, EventArgs e)
    {
        await ExecuteTestFlowAsync(isDryRun: true).ConfigureAwait(true);
    }

    private async Task ExecuteTestFlowAsync(bool isDryRun)
    {
        if (_isRunning)
        {
            return;
        }

        string domain = txtDomain.Text?.Trim() ?? string.Empty;
        string userName = txtUserName.Text?.Trim() ?? string.Empty;
        string password = txtPassword.Text ?? string.Empty;

        if (string.IsNullOrEmpty(domain))
        {
            MessageBox.Show(this, "도메인 또는 Domain Controller 호스트명을 입력해 주세요.", "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtDomain.Focus();
            return;
        }

        if (!isDryRun)
        {
            if (string.IsNullOrEmpty(userName))
            {
                MessageBox.Show(this, "User ID를 입력해 주세요.", "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUserName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show(this, "Password를 입력해 주세요.", "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPassword.Focus();
                return;
            }
        }

        AuthTestTarget selectedTarget = (cboTestTarget.SelectedItem as ComboBoxItem<AuthTestTarget>)?.Value ?? AuthTestTarget.Both;
        AuthTestMethod selectedMethod = (cboTestMethod.SelectedItem as ComboBoxItem<AuthTestMethod>)?.Value ?? AuthTestMethod.DirectoryEntry;

        var request = new AdAuthTestRequest
        {
            Domain = domain,
            LdapPort = (int)numLdapPort.Value,
            LdapsPort = (int)numLdapsPort.Value,
            UserName = userName,
            Password = password,
            TestTarget = selectedTarget,
            TestMethod = selectedMethod,
            SkipSecondOnCredentialFailure = chkPreventDuplicateFailure.Checked,
            IsDryRun = isDryRun
        };

        SetUiRunningState(true);
        ResetStatusSummary(request.TestTarget);

        AppendLog(new string('=', 70), Color.DarkGray);
        if (isDryRun)
        {
            AppendLog($"▶ [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 사전 포트 연결 점검(Dry-Run) 시작 (대상: {selectedTarget}, 실제 인증 생략)", Color.FromArgb(128, 255, 255));
            AppendLog($"  - 대상 도메인: {request.Domain} | LDAP Port: {request.LdapPort} | LDAPS Port: {request.LdapsPort}", Color.LightGray);
        }
        else
        {
            AppendLog($"▶ [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AD 인증 검증 시작 (대상: {selectedTarget}, 검증방식: {selectedMethod})", Color.FromArgb(255, 255, 128));
            AppendLog($"  - 대상 도메인: {request.Domain} | LDAP Port: {request.LdapPort} | LDAPS Port: {request.LdapsPort}", Color.LightGray);
            AppendLog($"  - 사용자 ID: {request.UserName} | 중복 실패 방지 모드: {(request.SkipSecondOnCredentialFailure ? "ON (자격증명 오류 시 즉시 중단)" : "OFF")}", Color.LightGray);
        }

        try
        {
            List<AdAuthTestResult> results = await _authChecker.RunAuthTestsAsync(request, result =>
            {
                BeginInvoke(new Action(() => UpdateProtocolResult(result)));
            }).ConfigureAwait(true);

            UpdateOverallSummary(results, isDryRun);
        }
        catch (Exception ex)
        {
            AppendLog($"[실행 중 치명적 오류] {ex.Message}", Color.Salmon);
            lblSummary.Text = "상태: 검증 중 예외가 발생했습니다.";
            lblSummary.ForeColor = Color.Red;
        }
        finally
        {
            SetUiRunningState(false);
            AppendLog($"◀ [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(isDryRun ? "사전 포트 점검(Dry-Run)" : "AD 인증 검증")} 완료", Color.FromArgb(255, 255, 128));
        }
    }

    private void SetUiRunningState(bool running)
    {
        _isRunning = running;
        btnRunTest.Enabled = !running;
        btnDryRun.Enabled = !running;
        btnRunTest.Text = running ? "⏳ 검증 진행 중..." : "▶ AD 인증 검증 실행";
        btnReloadEnv.Enabled = !running;
        txtDomain.Enabled = !running;
        txtUserName.Enabled = !running;
        txtPassword.Enabled = !running;
        numLdapPort.Enabled = !running;
        numLdapsPort.Enabled = !running;
        cboTestTarget.Enabled = !running;
        cboTestMethod.Enabled = !running;
        chkPreventDuplicateFailure.Enabled = !running;
    }

    private void ResetStatusSummary(AuthTestTarget target)
    {
        lblSummary.Text = "상태: 검증 진행 중...";
        lblSummary.ForeColor = Color.FromArgb(0, 102, 204);

        if (target == AuthTestTarget.Both || target == AuthTestTarget.LdapOnly)
        {
            lblLdapStatus.Text = "LDAP (389): [진행 중]";
            lblLdapStatus.ForeColor = Color.DarkOrange;
        }
        else
        {
            lblLdapStatus.Text = "LDAP (389): [비대상]";
            lblLdapStatus.ForeColor = Color.Gray;
        }

        if (target == AuthTestTarget.Both || target == AuthTestTarget.LdapsOnly)
        {
            lblLdapsStatus.Text = "LDAPS (636): [대기]";
            lblLdapsStatus.ForeColor = Color.Gray;
        }
        else
        {
            lblLdapsStatus.Text = "LDAPS (636): [비대상]";
            lblLdapsStatus.ForeColor = Color.Gray;
        }
    }

    private void UpdateProtocolResult(AdAuthTestResult result)
    {
        string statusText;
        Color statusColor;

        if (result.IsSkipped)
        {
            statusText = "[생략됨]";
            statusColor = Color.FromArgb(255, 165, 0);
            AppendLog($"  - [{result.Protocol}] ⏭ 건너뜀: {result.SkipReason}", Color.Orange);
        }
        else if (result.IsSuccess)
        {
            statusText = $"[성공 - {result.ElapsedMilliseconds}ms]";
            statusColor = Color.FromArgb(0, 204, 102);
            AppendLog($"  - [{result.Protocol}] ✔ 성공 ({result.ElapsedMilliseconds}ms): {result.DiagnosticDetail}", Color.FromArgb(120, 255, 120));
        }
        else
        {
            statusText = $"[실패 - {result.ElapsedMilliseconds}ms]";
            statusColor = Color.Salmon;
            AppendLog($"  - [{result.Protocol}] ✖ 실패 ({result.ElapsedMilliseconds}ms): {result.DiagnosticDetail}", Color.Salmon);
            if (!string.IsNullOrWhiteSpace(result.LdapDataCode))
            {
                AppendLog($"      ↳ LDAP Error Data Code: data {result.LdapDataCode}", Color.Pink);
            }
        }

        if (result.Protocol == AuthProtocol.LDAP)
        {
            lblLdapStatus.Text = $"LDAP (389): {statusText}";
            lblLdapStatus.ForeColor = statusColor;
        }
        else
        {
            lblLdapsStatus.Text = $"LDAPS (636): {statusText}";
            lblLdapsStatus.ForeColor = statusColor;
        }
    }

    private void UpdateOverallSummary(List<AdAuthTestResult> results, bool isDryRun)
    {
        bool hasSuccess = false;
        bool hasFailure = false;
        bool hasSkipped = false;

        foreach (AdAuthTestResult r in results)
        {
            if (r.IsSuccess)
            {
                hasSuccess = true;
            }
            if (!r.IsSuccess && !r.IsSkipped)
            {
                hasFailure = true;
            }
            if (r.IsSkipped)
            {
                hasSkipped = true;
            }
        }

        if (hasSuccess && !hasFailure)
        {
            lblSummary.Text = isDryRun
                ? "상태: [Dry-Run] 모든 대상 포트 TCP 연결 성공!"
                : "상태: 모든 대상 프로토콜 인증 성공!";
            lblSummary.ForeColor = Color.FromArgb(0, 153, 76);
        }
        else if (hasSuccess && hasFailure)
        {
            lblSummary.Text = "상태: 일부 프로토콜만 성공 (상세 로그 확인)";
            lblSummary.ForeColor = Color.DarkOrange;
        }
        else if (hasSkipped && !hasSuccess)
        {
            lblSummary.Text = "상태: 자격 증명 오류로 실패 및 후속 시도 중단 (계정 잠금 방지됨)";
            lblSummary.ForeColor = Color.Salmon;
        }
        else
        {
            lblSummary.Text = isDryRun ? "상태: [Dry-Run] 포트 연결 실패" : "상태: 인증 실패";
            lblSummary.ForeColor = Color.Red;
        }
    }

    private void AppendLog(string message, Color color)
    {
        if (rtbLog.InvokeRequired)
        {
            rtbLog.BeginInvoke(new Action(() => AppendLog(message, color)));
            return;
        }

        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor = color;
        rtbLog.AppendText(message + Environment.NewLine);
        rtbLog.SelectionColor = rtbLog.ForeColor;
        rtbLog.ScrollToCaret();
    }

    private void BtnClearLog_Click(object? sender, EventArgs e)
    {
        rtbLog.Clear();
    }

    private void BtnCopyLog_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(rtbLog.Text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(rtbLog.Text);
            MessageBox.Show(this, "로그가 클립보드에 복사되었습니다.", "복사 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"클립보드 복사 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class ComboBoxItem<T>(string display, T value)
    {
        public string Display { get; } = display;
        public T Value { get; } = value;

        public override string ToString()
        {
            return Display;
        }
    }
}
