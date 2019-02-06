using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FancyPacketProcessor
{
    public partial class MainForm : Form
    {
        public string PacketFolder { get; set; }

        public MainForm()
        {
            InitializeComponent();
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ( folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                PacketFolder = folderBrowserDialog1.SelectedPath;

                LoadAvailablePackets();
            }
        }

        private void LoadAvailablePackets()
        {
            this.packetFolderLabel.Text = String.Format("Current folder: {0}", PacketFolder);

            Directory.GetFiles(PacketFolder);
        }

    }
}
