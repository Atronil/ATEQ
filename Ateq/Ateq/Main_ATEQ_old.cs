using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System.IO.Ports;
using System.IO;
using Ini;


namespace Ateq
{
    public partial class Form1 : Form
    {
        // sql parameters
        string sql_connection = "";
        string sql_login = "";
        string sql_pass = "";
        string sql_db = "";
        string connectdb = null;
        string queryString = "";

        static readonly object locker = new object();

        int countpass = 0;
        int countall = 0;
        bool found_number = false;

        // limit parameters
        double press_minlimit = 0.0, press_maxlimit = 0.0, leak_minlimit = 0.0, leak_maxlimit = 0.0;

        DataTable tabledb = new DataTable();

        // serial parameters
        int baudrate = 0;
        string comport = null;

        string inifiledata = @"C:\Protech\ini\ateq.ini";

        SerialPort newport = new SerialPort();

        // program variables
        string username = null;
        bool PassedPreBond = false;
        bool PassedMainPCBA = false;
        bool PassedLeak = false;

        string[] portNames = SerialPort.GetPortNames();

        string serialnumber = null;


        public Form1()
        {
            InitializeComponent();
            textBox3.Text = DateTime.Now.ToString("dd/MM/yy");
            timer1.Tick += new EventHandler(Timer1_Tick);

            if (File.Exists(inifiledata))
            {
                // reading ini file
                INIFile inifile = new INIFile(inifiledata);

                // reading comport values
                comport = inifile.IniReadValue("SERIAL", "COMNAME");
                label15.Text = comport;
                baudrate = Convert.ToInt32(inifile.IniReadValue("SERIAL", "BAUDRATE"));

                // sql section
                sql_connection = inifile.IniReadValue("SQLCONNECTION", "CONNECTIONSTRING");
                sql_login = inifile.IniReadValue("SQLCONNECTION", "SQLUSER");
                sql_pass = inifile.IniReadValue("SQLCONNECTION", "SQLPASSWORD");
                sql_db = inifile.IniReadValue("SQLCONNECTION", "SQLDATABASE");
                // limit section
                press_minlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "PEAKMIN"));
                press_maxlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "PEAKMAX"));
                leak_minlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "LEAKMIN"));
                leak_maxlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "LEAKMAX"));

                // init of the datatable and passing values to datagridview
                this.tabledb.Columns.Add("Test Name");
                this.tabledb.Columns.Add("Min");
                this.tabledb.Columns.Add("Result");
                this.tabledb.Columns.Add("Max");
                this.tabledb.Columns.Add("Units");

                this.tabledb.Rows.Add("Pressure", press_minlimit, "0.0", press_maxlimit, "bar");
                this.tabledb.Rows.Add("Leakage", leak_minlimit, "0.0", leak_maxlimit, "Pa");
                this.tabledb.Rows.Add("Status");
                dataGridView1.DataSource = tabledb;

                // build the sql connection string
                connectdb = Connectstring(sql_connection, sql_login, sql_pass, sql_db);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectdb))
                    {
                        conn.Open();
                        label4.Visible = true;
                        label4.ForeColor = Color.Green;
                        label4.Text = "CONNECTED";
                    }

                    if (!newport.IsOpen)
                    {
                        try
                        {
                            Init_comport();
                            label16.ForeColor = Color.Green;
                            label16.Text = "Connected";
                            newport.Open();
                            cmd_Start.Enabled = false;
                            textBox2.Focus();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message + ex.StackTrace);
                        }

                    }
                    else
                    {
                        newport.Close();
                        label16.Text = "Closed";
                        cmd_Start.Enabled = false;
                        label12.Text = "Com Port Closed";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Info");
                }

            }
            else
            {
                MessageBox.Show("The ini file not exists, the Application will be closed", "MESSAGE");
                label4.Text = "Database Not connected to application";
                label4.Visible = true;
                label4.ForeColor = Color.Red;
            }

        }
        private string Connectstring(string conn, string user, string pass, string dbname)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = conn;   // update me
            builder.UserID = user;       // update me
            builder.Password = pass;     // update me
            builder.InitialCatalog = dbname;
            builder.IntegratedSecurity = true;  //whan connected via SQL Auth. must be FALSE!!!!

            return builder.ConnectionString;
        }

        private void cmd_Exit_Click(object sender, EventArgs e)
        {
            if (newport.IsOpen)
            {
                newport.Dispose();
                newport.Close();
            }
            Application.Exit();
        }


        private void SerialDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e)
        {
            string data = newport.ReadTo("\f"); //reads data till form feed sign
            richTextBox1.Invoke(new Action(() => { richTextBox1.AppendText(data); }));
            Find_Result(data, press_minlimit, press_maxlimit, leak_minlimit, leak_maxlimit, serialnumber);
            //richTextBox1.Invoke(new Action(() => { richTextBox1.Clear(); }));
        }
        /// <summary>
        /// Communication port initialization (com9, baud9600)
        /// </summary>
        private void Init_comport()
        {
            newport.Parity = Parity.None;
            newport.StopBits = StopBits.One;
            newport.DataBits = 7;   //7 for ATEQ
            newport.BaudRate = baudrate;
            newport.Handshake = Handshake.None;
            newport.PortName = comport;
            newport.ReadTimeout = 10000;

            //newport.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceivedEventHandler);
        }

        private void cmd_Start_Click(object sender, EventArgs e)
        {
            if (newport.IsOpen)
            {
                label12.Text = "SCAN SERIAL NUMBER";
                textBox1.Focus();
            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.ActiveControl = textBox2;
            textBox2.Focus();
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            this.textBox4.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        // Serial - textBox1
        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                serialnumber = textBox1.Text;
                Regex rx = new Regex(@"34\d{6}\Z");
                bool regexp = rx.IsMatch(serialnumber, 0);
                if (regexp)
                {
                    //found_number = SearchDB(connectdb, serialnumber);
                    found_number = true;
                    if (found_number == false)
                    {
                        label12.Text = "UNIT not passed PREBOND! Please return to PREVIOUS Station";
                        textBox1.Focus();
                        textBox1.Clear();
                    }
                    else
                    {
                        label12.Text = "UNIT Passed PREBOND! INSERT to ATEQ and PRESS 2 GREEN Buttons!";
                        newport.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceivedEventHandler);
                    }
                }
                else
                {
                    label12.Text = "SERIAL NUMBER IS WRONG";
                    textBox1.Focus();
                    textBox1.Clear();
                }
            }
        }
        /*
        private void Button1_Click(object sender, EventArgs e)
        {
            string test = "<01>: 0.609 bar:(OK):  012 Pa";

            Find_Result(test, press_minlimit, press_maxlimit, leak_minlimit, leak_maxlimit);
        }*/

        // User - textBox2
        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                username = textBox2.Text;
                cmd_Start.Enabled = true;
                label12.Text = "PRESS START";
                cmd_Start.Focus();
            }
        }

        private void Find_Result(string indata, double peakmin, double peakmax, double leakmix, double leakmax, string numsid)
        {
            double pa = 0.0;
            string press_result = getBetween(indata, "<01>: ", " bar");
            string status_result = getBetween(indata, "bar:(", "):");
            string leak_result = getBetween(indata, "): ", " Pa");
            if(leak_result.Contains("-"))
                pa = -1 * (Convert.ToDouble(leak_result) / 100);
            else
                pa = Convert.ToDouble(leak_result) / 100;

            double bar = Convert.ToDouble(press_result);
            bool final_pass = false;

            dataGridView1.Rows[0].Cells[2].Value = bar;
            dataGridView1.Rows[1].Cells[2].Value = pa;
            newport.DataReceived -= SerialDataReceivedEventHandler;
            //if ((peakmin <= bar) && (bar <= peakmax) && (leak_minlimit <= pa) && (pa <= leak_maxlimit))
            if(status_result.Equals("OK"))
            {
                dataGridView1.Rows[2].Cells[2].Style.BackColor = Color.Green;
                dataGridView1.Rows[2].Cells[2].Value = "PASS";
                final_pass = true;

                if (checkBox1.Checked)
                {
                    listBox1.Invoke(new EventHandler(delegate
                    {
                        listBox1.Items.Add(serialnumber + " - PASS (SAVED)");
                    }));/*
                    textBox5.Invoke(new EventHandler(delegate
                    {
                        textBox5.Text = (countpass++).ToString();
                    }));
                    textBox6.Invoke(new EventHandler(delegate
                    {
                        textBox6.Text = (countall++).ToString();
                    }));
                    // YTP = 100 - ( ( (tested - passes) / tested ) * 100)     (% result)
                    textBox7.Invoke(new EventHandler(delegate
                    {
                        textBox7.Text = (100 - (((countall - countpass) / countall) * 100)).ToString();
                    }));
                    */
                    int rest = SaveDB(connectdb, numsid, final_pass);
                    if (rest == 1)
                    {
                        
                        MessageBox.Show("PASS", "Info");
                        dataGridView1.Invoke(new EventHandler(delegate
                        {
                            dataGridView1.Rows[0].Cells[2].Value = "0.0";
                            dataGridView1.Rows[1].Cells[2].Value = "0.0";
                            dataGridView1.Rows[2].Cells[2].Style.BackColor = Color.White;
                            dataGridView1.Rows[2].Cells[2].Value = "";
                        }));
                        textBox1.Invoke(new EventHandler(delegate
                        {
                            textBox1.Focus();
                            textBox1.Clear();
                        }));
                        label12.Invoke(new EventHandler(delegate
                        {
                            label12.Text = "SCAN SERIAL NUMBER";
                        }));
                    }      
                    else
                    {
                        final_pass = false;
                        MessageBox.Show("FAIL", "Info");
                        dataGridView1.Invoke(new EventHandler(delegate
                        {
                            dataGridView1.Rows[0].Cells[2].Value = "0.0";
                            dataGridView1.Rows[1].Cells[2].Value = "0.0";
                            dataGridView1.Rows[2].Cells[2].Style.BackColor = Color.White;
                            dataGridView1.Rows[2].Cells[2].Value = "";
                        }));
                        textBox1.Invoke(new EventHandler(delegate
                        {
                            textBox1.Focus();
                            textBox1.Clear();
                        }));
                        label12.Invoke(new EventHandler(delegate
                        {
                            label12.Text = "SCAN SERIAL NUMBER";
                        }));
                    }                  

                    SaveLog(indata, numsid, final_pass);
                }
                else
                {
                    final_pass = true;
                    listBox1.Invoke(new EventHandler(delegate
                    {
                        listBox1.Items.Add(serialnumber + " - PASS (NOT SAVED)");
                    }));/*
                    textBox5.Invoke(new EventHandler(delegate
                    {
                        textBox5.Text = (countpass++).ToString();
                    }));
                    textBox6.Invoke(new EventHandler(delegate
                    {
                        textBox6.Text = (countall++).ToString();
                    }));
                    textBox7.Invoke(new EventHandler(delegate
                    {
                        textBox7.Text = (100 - (((countall - countpass) / countall) * 100)).ToString();
                    }));*/
                    SaveLog(indata, numsid, final_pass);
                }
            }
            else
            {
                dataGridView1.Rows[2].Cells[2].Style.BackColor = Color.Red;
                dataGridView1.Rows[2].Cells[2].Value = "FAIL";
                final_pass = false;
                listBox1.Invoke(new EventHandler(delegate
                {
                    listBox1.Items.Add(serialnumber + " - FAIL (NOT SAVED)");
                }));/*
                textBox5.Invoke(new EventHandler(delegate
                {
                    textBox5.Text = (countpass).ToString();
                }));
                textBox6.Invoke(new EventHandler(delegate
                {
                    textBox6.Text = (countall++).ToString();
                }));
                textBox7.Invoke(new EventHandler(delegate
                {
                    textBox7.Text = (100 - (((countall - countpass) / countall) * 100)).ToString();
                }));*/

                SaveLog(indata, numsid, final_pass);
            }
            indata = null;
        }

        private int SaveDB(string conndb, string numstrdb, bool bitpass)
        {
            int rp = 0;
            using (SqlConnection conn = new SqlConnection(conndb))
            {
                queryString = String.Format("UPDATE Device SET PassedLeak=@PassedLeak WHERE ExternalUnitID=@ExternalUnitID");
                conn.Open();
                SqlCommand query = new SqlCommand(queryString, conn);

                query.Parameters.AddWithValue("@PassedLeak", bitpass);
                query.Parameters.AddWithValue("@ExternalUnitID", numstrdb);
                //query.Parameters.AddWithValue("PassedPreBond", SqlDbType.Binary).Value = Convert.ToBoolean(bitpass);
                //query.Parameters.AddWithValue("ExternalUnitID", SqlDbType.VarChar).Value = serialnumber;

                rp = query.ExecuteNonQuery();

            }
            return rp;
        }

        private void SaveLog(string bufdata, string sn, bool res_bool)
        {
            string flogpath = @"C:\Leak Test\Log\";
            string logname = "";
                   
            if (res_bool)
                logname = serialnumber + "_PASSED_" + DateTime.Now.ToString("HH_mm") + ".log";
            else
                logname = serialnumber + "_FAILED_" + DateTime.Now.ToString("HH_mm") + ".log";

            try
            {
                if (!File.Exists(flogpath + logname))
                {
                    using (FileStream fs = File.Create(flogpath + logname))

                    using (StreamWriter w = new StreamWriter(fs))
                    { 
                        AppendLog(bufdata, w, serialnumber, res_bool);

                    }
                    
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Info");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            if (UncheckFlag(connectdb, serialnumber) == 1)
            {
                MessageBox.Show("PassedLeak Unchecked", "Info");
            }
            else
            {
                MessageBox.Show("Error Detected", "Info");
            }
        }

        private static void AppendLog(string logMessage, TextWriter txtWriter, string sernum, bool flagit)
        {
            try
            {
                txtWriter.WriteLine("-- S/N: {0}", sernum);
                if (flagit)
                    txtWriter.WriteLine("-- Result: PASSED");
                else
                    txtWriter.WriteLine("-- Result: FAILED");

                txtWriter.WriteLine("{0}\n", logMessage);
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message, "Info");
            }
        }

        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }
            else
            {
                return "";
            }
        }

        private bool SearchDB(string connectstr, string numstr)
        {
            using (SqlConnection conn = new SqlConnection(connectstr))
            {
                string queryString = @"SELECT * " + "FROM Device " + "WHERE ExternalUnitID = @ExternalUnitID";
                using (SqlCommand cmd = new SqlCommand(queryString, conn))
                {
                    cmd.CommandText = queryString;
                    cmd.Parameters.AddWithValue("@ExternalUnitID", numstr);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            PassedMainPCBA = (bool)reader["PassedMainPCBA"];
                            PassedPreBond = (bool)reader["PassedPreBond"];
                            PassedLeak = (bool)reader["PassedLeak"];
                        }
                    }
                    //PassedPreBond = false / true;
                    return PassedPreBond;
                }       
            }

        }

        private int UncheckFlag(string connectstr, string numstr)
        {
            int rpep = 0;
            using (SqlConnection conn = new SqlConnection(connectstr))
            {
                queryString = String.Format("UPDATE Device SET PassedLeak=@PassedLeak WHERE ExternalUnitID=@ExternalUnitID");
                conn.Open();
                SqlCommand query = new SqlCommand(queryString, conn);

                query.Parameters.AddWithValue("@PassedLeak", false);
                query.Parameters.AddWithValue("@ExternalUnitID", numstr);
                //query.Parameters.AddWithValue("PassedPreBond", SqlDbType.Binary).Value = Convert.ToBoolean(bitpass);
                //query.Parameters.AddWithValue("ExternalUnitID", SqlDbType.VarChar).Value = serialnumber;
                rpep = query.ExecuteNonQuery();
                
            }

            return rpep;
        }
       
    }

}
/*
 * 
 * queryString = string.Format("SELECT * FROM Device WHERE ExternalUnitID = {0}", numstr);
                //queryString = String.Format("SELECT COUNT(*) FROM Device WHERE ExternalUnitID = {0}", numstr);
                SqlCommand command = new SqlCommand(queryString, conn);
                reader = command.ExecuteReader();

                while(reader.Read())
                {
                    string InternalUnitID = reader["InternalUnitID"].ToString();
                    string IMEI = reader["IMEI"].ToString();
                    string SIMID = reader["SIMID"].ToString();
                }


    /* conn.Open();

                //queryString = String.Format("SELECT * FROM Device WHERE ExternalUnitID = {0}", numstr);
                queryString = "SELECT * FROM Device WHERE ExternalUnitID = " + numstr+';';
                SqlCommand command = new SqlCommand(queryString, conn);

                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string InternalUnitID = reader["InternalUnitID"].ToString();
                    string IMEI = reader["IMEI"].ToString();
                    string SIMID = reader["SIMID"].ToString();
                    result++;
                }


                */

 //ans = Convert.ToInt32(command.ExecuteScalar());
 /*
 if (ans.GetType() == typeof(DBNull))
 {
     result = 0;
     label12.ForeColor = Color.Red;
     label12.Text = "Serial NOT FOUND in DB, Check Again!";
     textBox1.Clear();
 }
 else
 {
     result = (int)ans;
     queryString = String.Format("SELECT * FROM [dbo].[Device] WHERE ExternalUnitID = {0}", numstr);
     command = new SqlCommand(queryString, conn);
     reader = command.ExecuteReader();
     while (reader.Read())
     {
         label12.ForeColor = Color.Green;
         label12.Text = "Serial number found";
         PassedLeak = (bool)reader["PassedLeak"];
         PassedPCBA = (bool)reader["PassedPCBA"];
     }
     if(PassedPCBA == false)
     {
         label12.Text = "UNIT not passed PCBA Station";
     }
     else
     {
         label12.Text = "UNIT Passed PCBA Station! PRESS two GREEN Buttons!";
     }
 }*/

