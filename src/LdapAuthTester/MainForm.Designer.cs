#nullable enable
namespace LdapAuthTester;

public partial class MainForm
{
    private readonly System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components is not null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.grpEnvironment = new System.Windows.Forms.GroupBox();
        this.btnReloadEnv = new System.Windows.Forms.Button();
        this.lblEnvDetails = new System.Windows.Forms.Label();
        this.lblDomainJoinStatus = new System.Windows.Forms.Label();
        this.grpSettings = new System.Windows.Forms.GroupBox();
        this.chkShowPassword = new System.Windows.Forms.CheckBox();
        this.chkPreventDuplicateFailure = new System.Windows.Forms.CheckBox();
        this.cboTestMethod = new System.Windows.Forms.ComboBox();
        this.lblTestMethod = new System.Windows.Forms.Label();
        this.cboTestTarget = new System.Windows.Forms.ComboBox();
        this.lblTestTarget = new System.Windows.Forms.Label();
        this.txtPassword = new System.Windows.Forms.TextBox();
        this.lblPassword = new System.Windows.Forms.Label();
        this.txtUserName = new System.Windows.Forms.TextBox();
        this.lblUserName = new System.Windows.Forms.Label();
        this.numLdapsPort = new System.Windows.Forms.NumericUpDown();
        this.lblLdapsPort = new System.Windows.Forms.Label();
        this.numLdapPort = new System.Windows.Forms.NumericUpDown();
        this.lblLdapPort = new System.Windows.Forms.Label();
        this.txtDomain = new System.Windows.Forms.TextBox();
        this.lblDomain = new System.Windows.Forms.Label();
        this.pnlActions = new System.Windows.Forms.Panel();
        this.btnClearLog = new System.Windows.Forms.Button();
        this.btnCopyLog = new System.Windows.Forms.Button();
        this.btnDryRun = new System.Windows.Forms.Button();
        this.btnRunTest = new System.Windows.Forms.Button();
        this.grpStatus = new System.Windows.Forms.GroupBox();
        this.lblLdapsStatus = new System.Windows.Forms.Label();
        this.lblLdapStatus = new System.Windows.Forms.Label();
        this.lblSummary = new System.Windows.Forms.Label();
        this.grpLog = new System.Windows.Forms.GroupBox();
        this.rtbLog = new System.Windows.Forms.RichTextBox();
        this.grpEnvironment.SuspendLayout();
        this.grpSettings.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numLdapsPort)).BeginInit();
        ((System.ComponentModel.ISupportInitialize)(this.numLdapPort)).BeginInit();
        this.pnlActions.SuspendLayout();
        this.grpStatus.SuspendLayout();
        this.grpLog.SuspendLayout();
        this.SuspendLayout();
        //
        // grpEnvironment
        //
        this.grpEnvironment.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.grpEnvironment.Controls.Add(this.btnReloadEnv);
        this.grpEnvironment.Controls.Add(this.lblEnvDetails);
        this.grpEnvironment.Controls.Add(this.lblDomainJoinStatus);
        this.grpEnvironment.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.grpEnvironment.Location = new System.Drawing.Point(12, 12);
        this.grpEnvironment.Name = "grpEnvironment";
        this.grpEnvironment.Size = new System.Drawing.Size(860, 78);
        this.grpEnvironment.TabIndex = 0;
        this.grpEnvironment.TabStop = false;
        this.grpEnvironment.Text = " 윈도우 로그인 및 AD 도메인 조인 상태 ";
        //
        // btnReloadEnv
        //
        this.btnReloadEnv.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnReloadEnv.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.btnReloadEnv.Location = new System.Drawing.Point(740, 24);
        this.btnReloadEnv.Name = "btnReloadEnv";
        this.btnReloadEnv.Size = new System.Drawing.Size(105, 38);
        this.btnReloadEnv.TabIndex = 2;
        this.btnReloadEnv.Text = "환경 재조회";
        this.btnReloadEnv.UseVisualStyleBackColor = true;
        this.btnReloadEnv.Click += new System.EventHandler(this.BtnReloadEnv_Click);
        //
        // lblEnvDetails
        //
        this.lblEnvDetails.AutoSize = true;
        this.lblEnvDetails.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblEnvDetails.ForeColor = System.Drawing.Color.DimGray;
        this.lblEnvDetails.Location = new System.Drawing.Point(15, 47);
        this.lblEnvDetails.Name = "lblEnvDetails";
        this.lblEnvDetails.Size = new System.Drawing.Size(161, 15);
        this.lblEnvDetails.TabIndex = 1;
        this.lblEnvDetails.Text = "도메인 및 사용자 정보 로딩 중...";
        //
        // lblDomainJoinStatus
        //
        this.lblDomainJoinStatus.AutoSize = true;
        this.lblDomainJoinStatus.Font = new System.Drawing.Font("맑은 고딕", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblDomainJoinStatus.ForeColor = System.Drawing.Color.FromArgb(0, 102, 204);
        this.lblDomainJoinStatus.Location = new System.Drawing.Point(15, 24);
        this.lblDomainJoinStatus.Name = "lblDomainJoinStatus";
        this.lblDomainJoinStatus.Size = new System.Drawing.Size(175, 17);
        this.lblDomainJoinStatus.TabIndex = 0;
        this.lblDomainJoinStatus.Text = "AD 도메인 상태 확인 중...";
        //
        // grpSettings
        //
        this.grpSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.grpSettings.Controls.Add(this.chkShowPassword);
        this.grpSettings.Controls.Add(this.chkPreventDuplicateFailure);
        this.grpSettings.Controls.Add(this.cboTestMethod);
        this.grpSettings.Controls.Add(this.lblTestMethod);
        this.grpSettings.Controls.Add(this.cboTestTarget);
        this.grpSettings.Controls.Add(this.lblTestTarget);
        this.grpSettings.Controls.Add(this.txtPassword);
        this.grpSettings.Controls.Add(this.lblPassword);
        this.grpSettings.Controls.Add(this.txtUserName);
        this.grpSettings.Controls.Add(this.lblUserName);
        this.grpSettings.Controls.Add(this.numLdapsPort);
        this.grpSettings.Controls.Add(this.lblLdapsPort);
        this.grpSettings.Controls.Add(this.numLdapPort);
        this.grpSettings.Controls.Add(this.lblLdapPort);
        this.grpSettings.Controls.Add(this.txtDomain);
        this.grpSettings.Controls.Add(this.lblDomain);
        this.grpSettings.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.grpSettings.Location = new System.Drawing.Point(12, 96);
        this.grpSettings.Name = "grpSettings";
        this.grpSettings.Size = new System.Drawing.Size(860, 160);
        this.grpSettings.TabIndex = 1;
        this.grpSettings.TabStop = false;
        this.grpSettings.Text = " 인증 및 AD 연결 설정 ";
        //
        // chkShowPassword
        //
        this.chkShowPassword.AutoSize = true;
        this.chkShowPassword.Font = new System.Drawing.Font("맑은 고딕", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.chkShowPassword.Location = new System.Drawing.Point(340, 92);
        this.chkShowPassword.Name = "chkShowPassword";
        this.chkShowPassword.Size = new System.Drawing.Size(49, 17);
        this.chkShowPassword.TabIndex = 9;
        this.chkShowPassword.Text = "표시";
        this.chkShowPassword.UseVisualStyleBackColor = true;
        this.chkShowPassword.CheckedChanged += new System.EventHandler(this.ChkShowPassword_CheckedChanged);
        //
        // chkPreventDuplicateFailure
        //
        this.chkPreventDuplicateFailure.AutoSize = true;
        this.chkPreventDuplicateFailure.Checked = true;
        this.chkPreventDuplicateFailure.CheckState = System.Windows.Forms.CheckState.Checked;
        this.chkPreventDuplicateFailure.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.chkPreventDuplicateFailure.ForeColor = System.Drawing.Color.DarkRed;
        this.chkPreventDuplicateFailure.Location = new System.Drawing.Point(400, 125);
        this.chkPreventDuplicateFailure.Name = "chkPreventDuplicateFailure";
        this.chkPreventDuplicateFailure.Size = new System.Drawing.Size(437, 19);
        this.chkPreventDuplicateFailure.TabIndex = 14;
        this.chkPreventDuplicateFailure.Text = "자격증명(비밀번호 등) 오류 시 후속 프로토콜 즉시 중단 (AD 계정 잠금 방지)";
        this.chkPreventDuplicateFailure.UseVisualStyleBackColor = true;
        //
        // cboTestMethod
        //
        this.cboTestMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cboTestMethod.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.cboTestMethod.FormattingEnabled = true;
        this.cboTestMethod.Location = new System.Drawing.Point(495, 88);
        this.cboTestMethod.Name = "cboTestMethod";
        this.cboTestMethod.Size = new System.Drawing.Size(342, 23);
        this.cboTestMethod.TabIndex = 13;
        //
        // lblTestMethod
        //
        this.lblTestMethod.AutoSize = true;
        this.lblTestMethod.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblTestMethod.Location = new System.Drawing.Point(400, 92);
        this.lblTestMethod.Name = "lblTestMethod";
        this.lblTestMethod.Size = new System.Drawing.Size(62, 15);
        this.lblTestMethod.TabIndex = 12;
        this.lblTestMethod.Text = "검증 방식:";
        //
        // cboTestTarget
        //
        this.cboTestTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cboTestTarget.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.cboTestTarget.FormattingEnabled = true;
        this.cboTestTarget.Location = new System.Drawing.Point(495, 57);
        this.cboTestTarget.Name = "cboTestTarget";
        this.cboTestTarget.Size = new System.Drawing.Size(342, 23);
        this.cboTestTarget.TabIndex = 11;
        //
        // lblTestTarget
        //
        this.lblTestTarget.AutoSize = true;
        this.lblTestTarget.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblTestTarget.Location = new System.Drawing.Point(400, 61);
        this.lblTestTarget.Name = "lblTestTarget";
        this.lblTestTarget.Size = new System.Drawing.Size(74, 15);
        this.lblTestTarget.TabIndex = 10;
        this.lblTestTarget.Text = "테스트 대상:";
        //
        // txtPassword
        //
        this.txtPassword.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.txtPassword.Location = new System.Drawing.Point(105, 88);
        this.txtPassword.Name = "txtPassword";
        this.txtPassword.Size = new System.Drawing.Size(225, 23);
        this.txtPassword.TabIndex = 8;
        this.txtPassword.UseSystemPasswordChar = true;
        //
        // lblPassword
        //
        this.lblPassword.AutoSize = true;
        this.lblPassword.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblPassword.Location = new System.Drawing.Point(15, 92);
        this.lblPassword.Name = "lblPassword";
        this.lblPassword.Size = new System.Drawing.Size(63, 15);
        this.lblPassword.TabIndex = 7;
        this.lblPassword.Text = "Password:";
        //
        // txtUserName
        //
        this.txtUserName.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.txtUserName.Location = new System.Drawing.Point(105, 57);
        this.txtUserName.Name = "txtUserName";
        this.txtUserName.Size = new System.Drawing.Size(275, 23);
        this.txtUserName.TabIndex = 6;
        //
        // lblUserName
        //
        this.lblUserName.AutoSize = true;
        this.lblUserName.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblUserName.Location = new System.Drawing.Point(15, 61);
        this.lblUserName.Name = "lblUserName";
        this.lblUserName.Size = new System.Drawing.Size(51, 15);
        this.lblUserName.TabIndex = 5;
        this.lblUserName.Text = "User ID:";
        //
        // numLdapsPort
        //
        this.numLdapsPort.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.numLdapsPort.Location = new System.Drawing.Point(740, 25);
        this.numLdapsPort.Maximum = new decimal([65535, 0, 0, 0]);
        this.numLdapsPort.Minimum = new decimal([1, 0, 0, 0]);
        this.numLdapsPort.Name = "numLdapsPort";
        this.numLdapsPort.Size = new System.Drawing.Size(97, 23);
        this.numLdapsPort.TabIndex = 4;
        this.numLdapsPort.Value = new decimal([636, 0, 0, 0]);
        //
        // lblLdapsPort
        //
        this.lblLdapsPort.AutoSize = true;
        this.lblLdapsPort.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblLdapsPort.Location = new System.Drawing.Point(650, 29);
        this.lblLdapsPort.Name = "lblLdapsPort";
        this.lblLdapsPort.Size = new System.Drawing.Size(78, 15);
        this.lblLdapsPort.TabIndex = 3;
        this.lblLdapsPort.Text = "LDAPS Port:";
        //
        // numLdapPort
        //
        this.numLdapPort.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.numLdapPort.Location = new System.Drawing.Point(495, 25);
        this.numLdapPort.Maximum = new decimal([65535, 0, 0, 0]);
        this.numLdapPort.Minimum = new decimal([1, 0, 0, 0]);
        this.numLdapPort.Name = "numLdapPort";
        this.numLdapPort.Size = new System.Drawing.Size(97, 23);
        this.numLdapPort.TabIndex = 2;
        this.numLdapPort.Value = new decimal([389, 0, 0, 0]);
        //
        // lblLdapPort
        //
        this.lblLdapPort.AutoSize = true;
        this.lblLdapPort.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblLdapPort.Location = new System.Drawing.Point(400, 29);
        this.lblLdapPort.Name = "lblLdapPort";
        this.lblLdapPort.Size = new System.Drawing.Size(70, 15);
        this.lblLdapPort.TabIndex = 1;
        this.lblLdapPort.Text = "LDAP Port:";
        //
        // txtDomain
        //
        this.txtDomain.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.txtDomain.Location = new System.Drawing.Point(105, 25);
        this.txtDomain.Name = "txtDomain";
        this.txtDomain.Size = new System.Drawing.Size(275, 23);
        this.txtDomain.TabIndex = 0;
        //
        // lblDomain
        //
        this.lblDomain.AutoSize = true;
        this.lblDomain.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblDomain.Location = new System.Drawing.Point(15, 29);
        this.lblDomain.Name = "lblDomain";
        this.lblDomain.Size = new System.Drawing.Size(83, 15);
        this.lblDomain.TabIndex = 0;
        this.lblDomain.Text = "Domain / DC:";
        //
        // pnlActions
        //
        this.pnlActions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.pnlActions.Controls.Add(this.btnClearLog);
        this.pnlActions.Controls.Add(this.btnCopyLog);
        this.pnlActions.Controls.Add(this.btnDryRun);
        this.pnlActions.Controls.Add(this.btnRunTest);
        this.pnlActions.Location = new System.Drawing.Point(12, 262);
        this.pnlActions.Name = "pnlActions";
        this.pnlActions.Size = new System.Drawing.Size(860, 48);
        this.pnlActions.TabIndex = 2;
        //
        // btnClearLog
        //
        this.btnClearLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnClearLog.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.btnClearLog.Location = new System.Drawing.Point(740, 6);
        this.btnClearLog.Name = "btnClearLog";
        this.btnClearLog.Size = new System.Drawing.Size(105, 36);
        this.btnClearLog.TabIndex = 3;
        this.btnClearLog.Text = "로그 지우기";
        this.btnClearLog.UseVisualStyleBackColor = true;
        this.btnClearLog.Click += new System.EventHandler(this.BtnClearLog_Click);
        //
        // btnCopyLog
        //
        this.btnCopyLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnCopyLog.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.btnCopyLog.Location = new System.Drawing.Point(620, 6);
        this.btnCopyLog.Name = "btnCopyLog";
        this.btnCopyLog.Size = new System.Drawing.Size(105, 36);
        this.btnCopyLog.TabIndex = 2;
        this.btnCopyLog.Text = "로그 복사";
        this.btnCopyLog.UseVisualStyleBackColor = true;
        this.btnCopyLog.Click += new System.EventHandler(this.BtnCopyLog_Click);
        //
        // btnDryRun
        //
        this.btnDryRun.BackColor = System.Drawing.Color.FromArgb(64, 128, 128);
        this.btnDryRun.Cursor = System.Windows.Forms.Cursors.Hand;
        this.btnDryRun.FlatAppearance.BorderSize = 0;
        this.btnDryRun.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnDryRun.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.btnDryRun.ForeColor = System.Drawing.Color.White;
        this.btnDryRun.Location = new System.Drawing.Point(260, 4);
        this.btnDryRun.Name = "btnDryRun";
        this.btnDryRun.Size = new System.Drawing.Size(230, 40);
        this.btnDryRun.TabIndex = 1;
        this.btnDryRun.Text = "🔍 사전 포트 점검 (Dry-Run)";
        this.btnDryRun.UseVisualStyleBackColor = false;
        this.btnDryRun.Click += new System.EventHandler(this.BtnDryRun_Click);
        //
        // btnRunTest
        //
        this.btnRunTest.BackColor = System.Drawing.Color.FromArgb(0, 122, 204);
        this.btnRunTest.Cursor = System.Windows.Forms.Cursors.Hand;
        this.btnRunTest.FlatAppearance.BorderSize = 0;
        this.btnRunTest.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnRunTest.Font = new System.Drawing.Font("맑은 고딕", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.btnRunTest.ForeColor = System.Drawing.Color.White;
        this.btnRunTest.Location = new System.Drawing.Point(0, 4);
        this.btnRunTest.Name = "btnRunTest";
        this.btnRunTest.Size = new System.Drawing.Size(250, 40);
        this.btnRunTest.TabIndex = 0;
        this.btnRunTest.Text = "▶ AD 인증 검증 실행";
        this.btnRunTest.UseVisualStyleBackColor = false;
        this.btnRunTest.Click += new System.EventHandler(this.BtnRunTest_Click);
        //
        // grpStatus
        //
        this.grpStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.grpStatus.Controls.Add(this.lblLdapsStatus);
        this.grpStatus.Controls.Add(this.lblLdapStatus);
        this.grpStatus.Controls.Add(this.lblSummary);
        this.grpStatus.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.grpStatus.Location = new System.Drawing.Point(12, 316);
        this.grpStatus.Name = "grpStatus";
        this.grpStatus.Size = new System.Drawing.Size(860, 60);
        this.grpStatus.TabIndex = 3;
        this.grpStatus.TabStop = false;
        this.grpStatus.Text = " 검증 결과 요약 ";
        //
        // lblLdapsStatus
        //
        this.lblLdapsStatus.AutoSize = true;
        this.lblLdapsStatus.Font = new System.Drawing.Font("맑은 고딕", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblLdapsStatus.ForeColor = System.Drawing.Color.Gray;
        this.lblLdapsStatus.Location = new System.Drawing.Point(530, 25);
        this.lblLdapsStatus.Name = "lblLdapsStatus";
        this.lblLdapsStatus.Size = new System.Drawing.Size(126, 17);
        this.lblLdapsStatus.TabIndex = 2;
        this.lblLdapsStatus.Text = "LDAPS (SSL): [대기]";
        //
        // lblLdapStatus
        //
        this.lblLdapStatus.AutoSize = true;
        this.lblLdapStatus.Font = new System.Drawing.Font("맑은 고딕", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblLdapStatus.ForeColor = System.Drawing.Color.Gray;
        this.lblLdapStatus.Location = new System.Drawing.Point(340, 25);
        this.lblLdapStatus.Name = "lblLdapStatus";
        this.lblLdapStatus.Size = new System.Drawing.Size(129, 17);
        this.lblLdapStatus.TabIndex = 1;
        this.lblLdapStatus.Text = "LDAP (389): [대기]";
        //
        // lblSummary
        //
        this.lblSummary.AutoSize = true;
        this.lblSummary.Font = new System.Drawing.Font("맑은 고딕", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.lblSummary.Location = new System.Drawing.Point(15, 25);
        this.lblSummary.Name = "lblSummary";
        this.lblSummary.Size = new System.Drawing.Size(193, 17);
        this.lblSummary.TabIndex = 0;
        this.lblSummary.Text = "상태: 검증 대기 중입니다.";
        //
        // grpLog
        //
        this.grpLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
        | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.grpLog.Controls.Add(this.rtbLog);
        this.grpLog.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
        this.grpLog.Location = new System.Drawing.Point(12, 382);
        this.grpLog.Name = "grpLog";
        this.grpLog.Size = new System.Drawing.Size(860, 317);
        this.grpLog.TabIndex = 4;
        this.grpLog.TabStop = false;
        this.grpLog.Text = " 상세 진단 및 실행 로그 ";
        //
        // rtbLog
        //
        this.rtbLog.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
        this.rtbLog.Dock = System.Windows.Forms.DockStyle.Fill;
        this.rtbLog.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.rtbLog.ForeColor = System.Drawing.Color.FromArgb(220, 220, 220);
        this.rtbLog.Location = new System.Drawing.Point(3, 19);
        this.rtbLog.Name = "rtbLog";
        this.rtbLog.ReadOnly = true;
        this.rtbLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
        this.rtbLog.Size = new System.Drawing.Size(854, 295);
        this.rtbLog.TabIndex = 0;
        this.rtbLog.Text = "";
        //
        // MainForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(884, 711);
        this.Controls.Add(this.grpLog);
        this.Controls.Add(this.grpStatus);
        this.Controls.Add(this.pnlActions);
        this.Controls.Add(this.grpSettings);
        this.Controls.Add(this.grpEnvironment);
        this.MinimumSize = new System.Drawing.Size(800, 600);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "LdapAuthTester — Active Directory LDAP / LDAPS 인증 및 연결 진단 도구";
        this.Load += new System.EventHandler(this.MainForm_Load);
        this.grpEnvironment.ResumeLayout(false);
        this.grpEnvironment.PerformLayout();
        this.grpSettings.ResumeLayout(false);
        this.grpSettings.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numLdapsPort)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.numLdapPort)).EndInit();
        this.pnlActions.ResumeLayout(false);
        this.grpStatus.ResumeLayout(false);
        this.grpStatus.PerformLayout();
        this.grpLog.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.GroupBox grpEnvironment = null!;
    private System.Windows.Forms.Label lblDomainJoinStatus = null!;
    private System.Windows.Forms.Label lblEnvDetails = null!;
    private System.Windows.Forms.Button btnReloadEnv = null!;
    private System.Windows.Forms.GroupBox grpSettings = null!;
    private System.Windows.Forms.TextBox txtDomain = null!;
    private System.Windows.Forms.Label lblDomain = null!;
    private System.Windows.Forms.Label lblLdapPort = null!;
    private System.Windows.Forms.NumericUpDown numLdapPort = null!;
    private System.Windows.Forms.Label lblLdapsPort = null!;
    private System.Windows.Forms.NumericUpDown numLdapsPort = null!;
    private System.Windows.Forms.Label lblUserName = null!;
    private System.Windows.Forms.TextBox txtUserName = null!;
    private System.Windows.Forms.Label lblPassword = null!;
    private System.Windows.Forms.TextBox txtPassword = null!;
    private System.Windows.Forms.Label lblTestTarget = null!;
    private System.Windows.Forms.ComboBox cboTestTarget = null!;
    private System.Windows.Forms.Label lblTestMethod = null!;
    private System.Windows.Forms.ComboBox cboTestMethod = null!;
    private System.Windows.Forms.CheckBox chkPreventDuplicateFailure = null!;
    private System.Windows.Forms.CheckBox chkShowPassword = null!;
    private System.Windows.Forms.Panel pnlActions = null!;
    private System.Windows.Forms.Button btnRunTest = null!;
    private System.Windows.Forms.Button btnDryRun = null!;
    private System.Windows.Forms.Button btnCopyLog = null!;
    private System.Windows.Forms.Button btnClearLog = null!;
    private System.Windows.Forms.GroupBox grpStatus = null!;
    private System.Windows.Forms.Label lblSummary = null!;
    private System.Windows.Forms.Label lblLdapStatus = null!;
    private System.Windows.Forms.Label lblLdapsStatus = null!;
    private System.Windows.Forms.GroupBox grpLog = null!;
    private System.Windows.Forms.RichTextBox rtbLog = null!;
}
