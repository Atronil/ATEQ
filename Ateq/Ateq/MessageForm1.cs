using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ateq
{
    public partial class MessageForm1 : Form
    {
        
        public MessageForm1(bool statmsg)
        {
            InitializeComponent();
            if (statmsg)
            {
                label1.ForeColor = Color.Green;
                label1.Text = "NETWORK CONNECTED";
            }
            else
            {
                label1.ForeColor = Color.Red;
                label1.Text = "NETWORK DISCONNECTED";
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Dispose();
        }
    }
}
