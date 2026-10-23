using BotReport2026.Config;

namespace BotReport2026.UI;

/// <summary>
/// Strip across the top of <see cref="MainForm"/> showing the working folder and letting
/// the user browse for another one. Every input, lookup and result folder lives inside it,
/// so changing it here re-points the whole app — MainForm does that work in
/// <see cref="MainForm.ApplyWorkspaceChange"/> when <see cref="RootChanged"/> fires.
/// </summary>
public class WorkspaceBar : UserControl
{
    private readonly TextBox _tbRoot;

    /// <summary>Raised after the user picks a different folder, with the folder that was
    /// in use before the change so reference files can be copied across.</summary>
    public event Action<string>? RootChanged;

    public WorkspaceBar()
    {
        Height = 38;
        Padding = new Padding(6, 4, 6, 4);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = "โฟลเดอร์ทำงาน:",
            AutoSize = true,
            Font = new Font("Tahoma", 9.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 5, 6, 0)
        }, 0, 0);

        // Read-only rather than disabled so the path stays selectable/copyable.
        _tbRoot = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Text = Workspace.Root,
            Margin = new Padding(0, 2, 6, 0)
        };
        layout.Controls.Add(_tbRoot, 1, 0);

        var btnBrowse = new Button { Text = "📁 เปลี่ยนโฟลเดอร์...", AutoSize = true, Margin = new Padding(0, 0, 4, 0) };
        btnBrowse.Click += (_, _) => Browse();
        layout.Controls.Add(btnBrowse, 2, 0);

        var btnOpen = new Button { Text = "📂 เปิด", AutoSize = true, Margin = new Padding(0) };
        btnOpen.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(Workspace.Root);
                System.Diagnostics.Process.Start("explorer.exe", Workspace.Root);
            }
            catch { }
        };
        layout.Controls.Add(btnOpen, 3, 0);
    }

    /// <summary>Re-reads the path from <see cref="Workspace"/> after it changed.</summary>
    public void RefreshPath() => _tbRoot.Text = Workspace.Root;

    private void Browse()
    {
        using var dlg = new FolderBrowserDialog
        {
            // Shown as the dialog title, so it has to stay short — the full sub-folder
            // list is on the hint line in the file-selection tab.
            Description = "เลือกโฟลเดอร์ทำงาน (โปรแกรมจะสร้างโฟลเดอร์ย่อยของแต่ละงานไว้ข้างใน)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(Workspace.Root) ? Workspace.Root : ""
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.Equals(Path.GetFullPath(dlg.SelectedPath), Workspace.Root,
                StringComparison.OrdinalIgnoreCase))
            return;

        string previousRoot = Workspace.Root;
        try
        {
            Workspace.SetRoot(dlg.SelectedPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ใช้โฟลเดอร์นี้ไม่ได้: {ex.Message}", "เปลี่ยนโฟลเดอร์ทำงานไม่สำเร็จ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        RefreshPath();
        RootChanged?.Invoke(previousRoot);
    }
}
