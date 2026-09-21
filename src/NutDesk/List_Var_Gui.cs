using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NutDesk.Common;
using static NutDesk.Common.AppGlobals;

namespace NutDesk
{
    public partial class List_Var_Gui : Form
    {
        private List<UPS_List_Datas> List_Var_Datas;
        private readonly UPS_Device UPSDevice;
        private readonly string UPS_Name = MainForm.Instance.UPS_Device?.Nut_Config?.UPSName ?? Properties.Settings.Default.NUT_UPSName;

        public List_Var_Gui(UPS_Device upsDev)
        {
            // This call is required by the designer.
            InitializeComponent();
            WireEvents();

            // Add any initialization after the InitializeComponent() call.
            UPSDevice = upsDev;
        }

        /// <summary>
        /// Replaces the VB "Handles" clauses. All event wiring lives here, never in the designer file.
        /// </summary>
        private void WireEvents()
        {
            Load += List_Var_Gui_Load;
            Timer_Update_List.Tick += Event_Update_List;
            Btn_Close.Click += Btn_Close_Click;
            Btn_Reload.Click += Btn_Reload_Click;
            TView_UPSVar.AfterSelect += TView_UPSVar_NodeChanged;
            Btn_Clip.Click += Btn_Clip_Click;
            Btn_Save.Click += Btn_Save_Click;
        }

        private void List_Var_Gui_Load(object sender, EventArgs e)
        {
            // Restyle on top of the designer layout (see NutDesk.Theming.Theme).
            Theming.Theme.Apply(this);
            LogFile.LogTracing("Load List Var Gui", LogLvl.LOG_DEBUG, this);
            Icon = MainForm.Instance.Icon;
            Visible = false;
            PopulateTreeView();
            Visible = true;
        }

        private void PopulateTreeView()
        {
            LogFile.LogTracing("Populate TreeView", LogLvl.LOG_DEBUG, this);
            Action action;

            if (UPSDevice == null)
            {
                LogFile.LogTracing("No UPS device available to list variables from.", LogLvl.LOG_WARNING, this);
                return;
            }

            // Pausing the poll while a LIST runs is deliberate, but it has to be restored no matter how this exits.
            // Previously an exception skipped the re-enable and the poll timer stayed stopped for the whole session.
            var wasUpdating = UPSDevice.IsUpdatingData;

            try
            {
                UPSDevice.IsUpdatingData = false;
                List_Var_Datas = UPSDevice.GetUPS_ListVar();
            }
            catch (Exception ex)
            {
                // Every other dialog raised from this window is localised; an English-only message here
                // leaves anyone running NutDesk in another culture unable to read what went wrong.
                MessageBox.Show(Properties.Resources.List_Var_Gui_GetVarsError + Environment.NewLine + ex.Message,
                                Properties.Resources.List_Var_Gui_GetVarsErrorCaption,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Close()
                return;
            }
            finally
            {
                // Restore the previous state rather than forcing it on: the device may have disconnected meanwhile.
                if (UPSDevice.IsConnected)
                {
                    UPSDevice.IsUpdatingData = wasUpdating;
                }
            }

            if (List_Var_Datas == null)
            {
                LogFile.LogTracing("ListUPSVars return Nothing Value", LogLvl.LOG_DEBUG, this);
                return;
            }

            action = () => TView_UPSVar.Nodes.Clear();
            TView_UPSVar.Invoke(action);
            action = () => TView_UPSVar.Nodes.Add(Properties.Settings.Default.NUT_UPSName, Properties.Settings.Default.NUT_UPSName);
            TView_UPSVar.Invoke(action);
            TreeNode TreeChild = new TreeNode();
            TreeNode LastNode = new TreeNode();
            foreach (var UPS_Var in List_Var_Datas)
            {
                LastNode = TView_UPSVar.Nodes[0];
                var FullPathNode = string.Empty;
                foreach (var SubPath in UPS_Var.VarKey.Split('.'))
                {
                    FullPathNode += SubPath + ".";
                    var Nodes = TView_UPSVar.Nodes.Find(FullPathNode, true);
                    if (Nodes.Length == 0)
                    {
                        if (LastNode.Text == "")
                        {
                            action = () => LastNode = TView_UPSVar.Nodes.Add(FullPathNode, SubPath);
                            TView_UPSVar.Invoke(action);
                        }
                        else
                        {
                            action = () => LastNode = LastNode.Nodes.Add(FullPathNode, SubPath);
                            TView_UPSVar.Invoke(action);
                        }
                    }
                    else
                    {
                        LastNode = Nodes[0];
                    }
                }
            }
        }

        private TreeNode FindNodeByValue(string value, TreeNodeCollection nodes)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Text == value)
                {
                    return n;
                }
                else
                {
                    //Recursively call the Function
                    TreeNode nodeToFind = FindNodeByValue(value, n.Nodes);
                    if (nodeToFind != null)
                    {
                        return nodeToFind;
                    }
                }
            }

            return null;
        }

        private void Event_Update_List(object sender, EventArgs e)
        {
            TreeNode SelectedNode = TView_UPSVar.SelectedNode;
            if (SelectedNode != null)
            {
                if (SelectedNode.Parent != null)
                {
                    if (SelectedNode.Parent.Text != UPS_Name && SelectedNode.Nodes.Count == 0)
                    {
                        var VarName = TView_UPSVar.SelectedNode.FullPath.Replace(UPS_Name + ".", "");
                        LogFile.LogTracing("Update {VarName}", LogLvl.LOG_DEBUG, this);
                        // The connection can drop while this window is open.
                        if (UPSDevice != null && UPSDevice.IsConnected)
                        {
                            Lbl_V_Value.Text = UPSDevice.GetUPSVar(VarName);
                        }
                    }
                }
            }
        }

        private void Btn_Close_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Close List Var Gui", LogLvl.LOG_DEBUG, this);
            Close();
        }

        private void Btn_Reload_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Reload Treeview from Button", LogLvl.LOG_DEBUG, this);
            Lbl_N_Value.Text = "";
            Lbl_V_Value.Text = "";
            Lbl_D_Value.Text = "";
            TView_UPSVar.Nodes.Clear();
            PopulateTreeView();
        }

        private void TView_UPSVar_NodeChanged(object sender, TreeViewEventArgs e)
        {
            int index = 0;
            var UPSName = Properties.Settings.Default.NUT_UPSName;
            // VB called Strings.Replace(..., Start:=1, Count:=1), which replaces only the first occurrence.
            var prefix = UPSName + ".";
            var prefixIndex = e.Node.FullPath.IndexOf(prefix, StringComparison.Ordinal);
            var SelectedChild = prefixIndex < 0 ? e.Node.FullPath : e.Node.FullPath.Remove(prefixIndex, prefix.Length);
            Predicate<UPS_List_Datas> FindChild = x =>
            {
                if (x.VarKey == SelectedChild)
                {
                    return true;
                }
                else
                {
                    index += 1;
                    return false;
                }
            };
            if (List_Var_Datas != null && !(SelectedChild == UPSName) &&
               List_Var_Datas.FindIndex(FindChild) != -1)
            {
                LogFile.LogTracing("Select {List_Var_Datas.Item(index).VarKey} Node", LogLvl.LOG_DEBUG, this);
                Lbl_N_Value.Text = List_Var_Datas[index].VarKey;
                Lbl_V_Value.Text = List_Var_Datas[index].VarValue;
                Lbl_D_Value.Text = List_Var_Datas[index].VarDesc;
            }
            else
            {
                Lbl_N_Value.Text = "";
                Lbl_V_Value.Text = "";
                Lbl_D_Value.Text = "";
            }
        }

        private string SerializeUPSData()
        {
            LogFile.LogTracing("Serializing UPS data to String.", LogLvl.LOG_DEBUG, this);
            var sb = new StringBuilder();

            if (UPSDevice == null || UPSDevice.UPS_Datas == null)
            {
                return sb.ToString();
            }

            // VB used a "With UPSDevice.UPS_Datas" block here.
            var upsDatas = UPSDevice.UPS_Datas;
            sb.AppendLine(UPSDevice.Name + " (" + upsDatas.Mfr + "/" + upsDatas.Model + "/" + upsDatas.Firmware + ")");

            if (List_Var_Datas == null)
            {
                // The variable list never loaded; the device header above is all there is to report.
                return sb.ToString();
            }

            foreach (var LDatas in List_Var_Datas)
            {
                sb.AppendLine(LDatas.VarKey + " (" + LDatas.VarDesc + ") : " + LDatas.VarValue);
            }

            LogFile.LogTracing("Successfully built serialized string, length: " + sb.Length, LogLvl.LOG_DEBUG, this);
            return sb.ToString();
        }

        private void Btn_Clip_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Copy TreeView To Clipboard", LogLvl.LOG_DEBUG, this);

            try
            {
                Clipboard.SetText(SerializeUPSData());
                LogFile.LogTracing("Successfully copied UPS information to the Clipboard.", LogLvl.LOG_NOTICE, this,
                                    Properties.Resources.List_Var_Gui__SetCpbTextSuccess);
            }
            catch (Exception ex)
            {
                var frmtdError = string.Format(Properties.Resources.List_Var_Gui__SetCpbTextError_Text, ex.Message);
                LogFile.LogTracing("Exception encountered while attempting to set Clipboard text.", LogLvl.LOG_ERROR, this,
                                frmtdError);
                LogFile.LogException(ex, this);
                MessageBox.Show(frmtdError, Properties.Resources.List_Var_Gui__SetCpbTextError_Caption,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Btn_Save_Click(object sender, EventArgs e)
        {
            LogFile.LogTracing("Export TreeView To File", LogLvl.LOG_DEBUG, this);

            var sfd = new SaveFileDialog
            {
                // The file type names sit in the same dialog as the localised title, so they follow the UI culture too.
                Filter = Properties.Resources.FileFilter_TextFiles,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Title = Properties.Resources.List_Var_Gui__SaveFile_Caption
            };
            var dialogRes = sfd.ShowDialog();

            if (dialogRes == DialogResult.OK && sfd.FileName != string.Empty)
            {
                LogFile.LogTracing("User completed SaveFileDialog, path: " + sfd.FileName, LogLvl.LOG_NOTICE, this);
                try
                {
                    using (var sw = new StreamWriter(sfd.OpenFile()))
                    {
                        sw.Write(SerializeUPSData());
                        sw.Close();
                        LogFile.LogTracing("File saved successfully.", LogLvl.LOG_NOTICE, this,
                                            string.Format(Properties.Resources.List_Var_Gui__SaveFileSuccess, sfd.FileName));
                    }
                }
                catch (Exception ex)
                {
                    var frmtdError = string.Format(Properties.Resources.List_Var_Gui__SaveFileError_Text, ex.Message);
                    LogFile.LogTracing("Exception encountered while saving UPS data to a file.", LogLvl.LOG_ERROR, this,
                                        frmtdError);
                    LogFile.LogException(ex, this);
                    MessageBox.Show(frmtdError, Properties.Resources.List_Var_Gui__SaveFile_Caption,
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                LogFile.LogTracing("SaveFileDialog was not accepted.", LogLvl.LOG_NOTICE, this);
            }
        }

        public List<string> GetChildren(TreeNode parentNode)
        {
            List<string> nodes = new List<string>();
            GetAllChildren(parentNode, nodes);
            return nodes;
        }

        public void GetAllChildren(TreeNode parentNode, List<string> nodes)
        {
            foreach (TreeNode childNode in parentNode.Nodes)
            {
                nodes.Add(childNode.Text);
                GetAllChildren(childNode, nodes);
            }
        }
    }
}
