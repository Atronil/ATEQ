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
    public partial class MessageForm : Form
    {
        /*ShowMyDialogBox Status
         * 0 - FAIL
         * 1 - PASS
         * 2 - Network Disconnected
         * 3 - Network Connected
         * 
         * 
         */
        public MessageForm(int ts)
        {
            InitializeComponent();

            switch (ts)
            {
                case 0:
                    label1.Font = new Font("Serif", 56, FontStyle.Bold); 
                    label1.ForeColor = Color.Red;
                    label1.Text = "FAIL";
                    break;
                case 1:
                    label1.Font = new Font("Serif", 56, FontStyle.Bold);
                    label1.ForeColor = Color.Green;
                    label1.Text = "PASS";
                    break;
                case 2:
                    label1.Font = new Font("Serif", 24, FontStyle.Bold); //Microsoft Sans Serif
                    label1.ForeColor = Color.Red;
                    label1.Text = "Network Disconnected";
                    break;
                case 3:
                    label1.Font = new Font("Serif", 24, FontStyle.Bold); //Microsoft Sans Serif
                    label1.ForeColor = Color.Green;
                    label1.Text = "Network Connected";
                    break;

            }
            
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Dispose();
        }
    }
}
