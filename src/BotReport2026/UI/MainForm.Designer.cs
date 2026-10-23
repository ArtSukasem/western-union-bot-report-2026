namespace BotReport2026.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TabControl _tabControl;
    private TabPage _tpFiles, _tpSettings, _tpRun, _tpExplain;
    private TabFileSelection _tabFileSelection;
    private TabSettings _tabSettings;
    private TabRunOutput _tabRunOutput;
    private TabExplain _tabExplain;
    private WorkspaceBar _workspaceBar;
    private MenuStrip _menuStrip;
    private ToolStripMenuItem _menuFile, _menuSaveConfig, _menuLoadConfig;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Font = new Font("Tahoma", 9.5f);

        _menuStrip = new MenuStrip();
        _menuFile = new ToolStripMenuItem("ไฟล์");
        _menuSaveConfig = new ToolStripMenuItem("บันทึกตั้งค่า");
        _menuLoadConfig = new ToolStripMenuItem("โหลดตั้งค่า");
        _menuFile.DropDownItems.Add(_menuSaveConfig);
        _menuFile.DropDownItems.Add(_menuLoadConfig);
        _menuStrip.Items.Add(_menuFile);
        MainMenuStrip = _menuStrip;

        _menuSaveConfig.Click += (_, _) => { _config = _tabSettings.GetConfig(); BotReport2026.Config.ConfigManager.Save(_config); MessageBox.Show("บันทึกตั้งค่าเรียบร้อย", "บันทึก"); };
        _menuLoadConfig.Click += (_, _) => { _config = BotReport2026.Config.ConfigManager.Load(); _tabSettings.SetConfig(_config); MessageBox.Show("โหลดตั้งค่าเรียบร้อย", "โหลด"); };

        _tabFileSelection = new TabFileSelection();
        _tabSettings = new TabSettings();
        _tabRunOutput = new TabRunOutput(this);
        _tabExplain = new TabExplain(this);

        _tpFiles = new TabPage("📁  เลือกไฟล์") { Padding = new Padding(6) };
        _tpFiles.Controls.Add(_tabFileSelection);
        _tabFileSelection.Dock = DockStyle.Fill;

        _tpSettings = new TabPage("⚙️  ตั้งค่า") { Padding = new Padding(6) };
        _tpSettings.Controls.Add(_tabSettings);
        _tabSettings.Dock = DockStyle.Fill;

        _tpRun = new TabPage("▶  รันและผลลัพธ์") { Padding = new Padding(6) };
        _tpRun.Controls.Add(_tabRunOutput);
        _tabRunOutput.Dock = DockStyle.Fill;

        _tpExplain = new TabPage("🔍  ตรวจสอบรายบุคคล") { Padding = new Padding(6) };
        _tpExplain.Controls.Add(_tabExplain);
        _tabExplain.Dock = DockStyle.Fill;

        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.TabPages.AddRange(new[] { _tpFiles, _tpSettings, _tpRun, _tpExplain });

        _workspaceBar = new WorkspaceBar { Dock = DockStyle.Top };

        // Added back-to-front: the last Add sits topmost among Top-docked controls, so
        // the menu ends up above the workspace bar, which sits above the tabs.
        Controls.Add(_tabControl);
        Controls.Add(_workspaceBar);
        Controls.Add(_menuStrip);
    }
}
