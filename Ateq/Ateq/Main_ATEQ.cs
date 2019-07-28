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
using System.Diagnostics;

namespace Ateq
{
    public partial class Form1 : Form
    {
        // sql parameters
        string sql_connection = "";
        string sql_login = "";
        string sql_pass = "";
        string sql_db = "";
        int manufacturer = 0;
        bool sql_access = false;
        string connectdb = null;
        string queryString = "";

        bool leakstatus = false;
                
        static readonly object locker = new object();
        
        bool found_number = false;

        // limit parameters
        double press_minlimit = 0.0, press_maxlimit = 0.0, leak_minlimit = 0.0, leak_maxlimit = 0.0;

        DataTable tabledb = new DataTable();

        // serial parameters
        int baudrate = 0;
        string comport = null;

        static string inifiledata = @"C:\Protech\ini\ateq.ini";

        SerialPort newport = new SerialPort();

        // program variables
        string username = null;
        string pncfg = null;
        bool PassedPreBond = false;
        bool PassedMainPCBA = false;
        bool PassedLeak = false;
        string pcName = null;

        string[] portNames = SerialPort.GetPortNames();

        string serialnumber = null;
        string proccessname = null;
        string version = null;

        System.Timers.Timer aTimer;

        string fileversion_str = null;
        public Form1()
        {
            InitializeComponent();
            

            textBox3.Text = DateTime.Now.ToString("dd/MM/yy");
            timer1.Tick += new EventHandler(Timer1_Tick);
            
            SetTimer(); // start timer query for connection to sql server 10 seconds interval

            //get Assembly version name
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            version = fvi.FileVersion;
            // Get computer name
            pcName = System.Windows.Forms.SystemInformation.ComputerName;

            // Get proccess name
            proccessname = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            fileversion_str = (proccessname + '_' + version).ToUpper();

            // Update Main Form Title
            this.Text += fileversion_str;
            this.Update();
            
            System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += AvailabilityChanged;
            
            if (File.Exists(inifiledata))
            {
                // reading ini file
                INIFile inifile = new INIFile(inifiledata);

                // reading comport values
                comport = inifile.IniReadValue("SERIAL", "COMNAME");
                label15.Text = comport;
                baudrate = Convert.ToInt32(inifile.IniReadValue("SERIAL", "BAUDRATE"));
                manufacturer = Convert.ToInt32(inifile.IniReadValue("COMPANY", "MANUFACTURER")); // 1 - IONICS, 2 - PM
                // sql section
                sql_connection = inifile.IniReadValue("SQLCONNECTION", "CONNECTIONSTRING");
                sql_login = inifile.IniReadValue("SQLCONNECTION", "SQLUSER");
                sql_pass = inifile.IniReadValue("SQLCONNECTION", "SQLPASSWORD");
                sql_db = inifile.IniReadValue("SQLCONNECTION", "SQLDATABASE");
                sql_access = bool.Parse(inifile.IniReadValue("SQLCONNECTION", "SQLACCESS"));
                // limit section
                press_minlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "PEAKMIN"));
                press_maxlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "PEAKMAX"));
                leak_minlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "LEAKMIN"));
                leak_maxlimit = Convert.ToDouble(inifile.IniReadValue("LIMITS", "LEAKMAX"));

                // init of the datatable and passing values to datagridview
                this.tabledb.Columns.Add("Test Name");
                //this.tabledb.Columns.Add("Min");
                this.tabledb.Columns.Add("Result");
                //this.tabledb.Columns.Add("Max");
                this.tabledb.Columns.Add("Units");

                //this.tabledb.Rows.Add("Pressure", press_minlimit, "0.0", press_maxlimit, "bar");
                this.tabledb.Rows.Add("Pressure", "0.0", "bar");
                //this.tabledb.Rows.Add("Leakage", leak_minlimit, "0.0", leak_maxlimit, "Pa");
                this.tabledb.Rows.Add("Leakage", "0.0", "Pa");
                this.tabledb.Rows.Add("Status");
                dataGridView1.DataSource = tabledb;

                // build the sql connection string
                connectdb = Connectstring(sql_connection, sql_login, sql_pass, sql_db);
                try
                {
                    // label status update db access
                    using (SqlConnection conn = new SqlConnection(connectdb))
                    {
                        conn.Open();
                        label4.Visible = true;
                        label4.ForeColor = Color.Green;
                        label4.Text = "CONNECTED";
                    }

                    //Label status update  com port
                    if (!newport.IsOpen)
                    {

                            Init_comport();
                            label16.ForeColor = Color.Green;
                            label16.Text = "CONNECTED";
                            newport.Open();
                            cmd_Start.Enabled = false;
                            textBox2.Focus();
                    }
                    else
                    {
                            newport.Close();
                            label16.Text = "DISCONNECTED";
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
                label4.Text = "Not Connected";
                label4.Visible = true;
                label4.ForeColor = Color.Red;
            }
        }
        
        /// <summary>
        /// Test that the server is connected
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <returns>true if the connection is opened</returns>
        private int IsServerConnected(string connectionString)
        {
            int retval = 0;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // create a SqlCommand object
                    SqlCommand mySqlCommand = connection.CreateCommand();

                    // run a PRINT statement
                    mySqlCommand.CommandText = "SELECT 1 FROM DeviceTestHistory";

                    return retval = mySqlCommand.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                return retval = 2; //error for connection to db
            }
            
        }

        private void DisableTimer()
        {
            aTimer.Elapsed -= new System.Timers.ElapsedEventHandler(aTimer_Elapsed);
            aTimer.Enabled = false;
        }
        private void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new System.Timers.Timer(10000);
            aTimer.Elapsed += new System.Timers.ElapsedEventHandler(aTimer_Elapsed);
            // Hook up the Elapsed event for the timer. 
            aTimer.Enabled = true;
        }

        private void aTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (IsServerConnected(connectdb) > -1)
            {
                label4.Invoke(new EventHandler(delegate
                {
                    label4.ForeColor = Color.Red;
                    label4.Text = "DISCONNECTED";
                }));
            }
            else
            {
                label4.Invoke(new EventHandler(delegate
                {
                    label4.ForeColor = Color.Green;
                    label4.Text = "CONNECTED";
                }));
            }
        }
        private void AvailabilityChanged(object sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable)
            {
                cmd_Start.Invoke(new EventHandler(delegate
                {
                    cmd_Start.Enabled = true;
                }));
                label12.Invoke(new EventHandler(delegate
                {
                    label12.ForeColor = Color.Black;
                    label12.Text = "SCAN SERIAL NUMBER";
                }));
                label4.Invoke(new EventHandler(delegate
                {
                    label4.ForeColor = Color.Green;
                    label4.Text = "CONNECTED";
                }));
                textBox1.Invoke(new EventHandler(delegate
                {
                    textBox1.Enabled = true;
                    textBox1.Focus();
                    textBox1.Clear();
                }));
                
                ShowMyDialogBox(3); //Network Connected
                SetTimer();
            }
            else
            {
                cmd_Start.Invoke(new EventHandler(delegate
                {
                    cmd_Start.Enabled = false;
                }));
                label12.Invoke(new EventHandler(delegate
                {
                    label12.ForeColor = Color.Black;
                    label12.Text = "NETWORK DISCONNECTED";
                }));
                label4.Invoke(new EventHandler(delegate
                {
                    label4.ForeColor = Color.Red;
                    label4.Text = "Not Connected";
                }));
                textBox1.Invoke(new EventHandler(delegate
                {
                    textBox1.Clear();
                    textBox1.Enabled = false;
                }));
                DisableTimer();
                ShowMyDialogBox(2); //Network Disconnected
            }
        }
        // Configuration of SQL String
        private string Connectstring(string conn, string user, string pass, string dbname)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = conn;   // update me
            builder.UserID = user;       // update me
            builder.Password = pass;     // update me
            builder.InitialCatalog = dbname;
            builder.IntegratedSecurity = sql_access;  //when connected via SQL Auth. must be FALSE!!!!

            return builder.ConnectionString;
        }
        // Exit from Application 
        private void cmd_Exit_Click(object sender, EventArgs e)
        {
            if (newport.IsOpen)
            {
                newport.Dispose();
                newport.Close();
            }
            Application.Exit();
        }
        // Serial Event Handler and reading the serial data
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
                label12.ForeColor = Color.Black;
                label12.Text = "SCAN SERIAL NUMBER";
                textBox1.Focus();
            }
            else
            {
                MessageBox.Show("The Comm Port is NOT CONNECTED", "Info");
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
        /*ShowMyDialogBox Status
         * 0 - FAIL
         * 1 - PASS
         * 2 - Network Disconnected
         * 3 - Network Connected
         */
        public void ShowMyDialogBox(int state_res)
        {
            MessageForm testDialog = new MessageForm(state_res);
                
            // Show testDialog as a modal dialog and determine if DialogResult = OK.
            if (testDialog.ShowDialog() == DialogResult.OK)
            {
                testDialog.Dispose();
            }
        }
        // Serial - textBox1
        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                if(string.IsNullOrEmpty(textBox2.Text) || string.IsNullOrEmpty(textBox5.Text))
                //if (textBox2.Enabled == true && textBox5.Enabled == true)
                {
                    MessageBox.Show("Some data missing please return to operator name", "Info");
                    textBox2.Enabled = true;
                    textBox5.Enabled = true;
                    textBox1.Clear();
                    textBox5.Clear();
                    textBox2.Focus();
                    textBox2.Clear();
                }
                else
                {

                    serialnumber = textBox1.Text;

                    //ShowMyDialogBox(false);
                    Regex rx = new Regex(@"34\d{6}\Z");
                    // NULL values before reading db data
                    PassedMainPCBA = false;
                    PassedPreBond = false;
                    PassedLeak = false;
                    leakstatus = false;
                    bool regexp = rx.IsMatch(serialnumber, 0);

                    if (regexp)
                    {
                        found_number = SearchDB(connectdb, serialnumber, out leakstatus);

                        if (found_number == false)
                        {
                            label12.ForeColor = Color.Red;
                            label12.Text = "UNIT NOT PASSED PREBOND! Please return to PREVIOUS Station";
                            textBox1.Focus();
                            textBox1.Clear();
                        }
                        else if (found_number == true && leakstatus == false)
                        {
                            label12.ForeColor = Color.Green;
                            label12.Text = "UNIT Passed PREBOND! INSERT to ATEQ and PRESS 2 GREEN Buttons!";
                            newport.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceivedEventHandler);
                        }
                        else if (found_number == true && leakstatus == true)
                        {
                            label12.ForeColor = Color.Red;
                            label12.Text = "ALERT!!! UNIT Passed ATEQ STATION!";
                            var answer_t = MessageBox.Show("THE UNIT PASSED ATEQ STATION,\n DO YOU WANT TO TEST IT AGAIN?", "Info", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (answer_t == DialogResult.Yes)
                            {
                                int status_clear = UncheckFlag(connectdb, serialnumber);
                                label12.ForeColor = Color.Green;
                                label12.Text = "UNIT Passed PREBOND! INSERT to ATEQ and PRESS 2 GREEN Buttons!";
                                newport.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceivedEventHandler);
                            }
                            else
                            {
                                label12.ForeColor = Color.Black;
                                label12.Text = "SCAN SERIAL NUMBER";
                                textBox1.Focus();
                                textBox1.Clear();

                            }

                        }
                    }
                    else
                    {
                        label12.ForeColor = Color.Black;
                        label12.Text = "SERIAL NUMBER IS WRONG";
                        textBox1.Focus();
                        textBox1.Clear();
                    }
                }
            }
        }
        // User - textBox2
        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                username = textBox2.Text;
                textBox2.Enabled = false;
                label12.ForeColor = Color.Black;
                label12.Text = "SCAN TOP CONFIG PART NUMBER";
                textBox5.Focus();
            }
        }
        // Top Level Part Number
        private void textBox5_KeyPress(object sender, KeyPressEventArgs e)
        {
            //063450002
            if (e.KeyChar == 13)
            {
                pncfg = textBox5.Text;
                Regex rx = new Regex(@"\d{9}\Z");
                bool regexp = rx.IsMatch(pncfg, 0);
                if(regexp)
                {
                    
                    textBox5.Enabled = false;
                    label12.ForeColor = Color.Black;
                    label12.Text = "PRESS START";
                    cmd_Start.Enabled = true;
                    cmd_Start.Focus();
                }
                else
                {
                    MessageBox.Show("TOP PN MUST BE 9 digits lenght", "Info");
                    textBox5.Clear();
                    textBox5.Focus();
                }
                
            }
        }
        private void Find_Result(string indata, double peakmin, double peakmax, double leakmix, double leakmax, string numsid)
        {
            double pa = 0.0;
            int final_pass = 0; 
            newport.DataReceived -= SerialDataReceivedEventHandler;

            if (indata.Contains("OK"))
            {
                string press_result = getBetween(indata, "<01>: ", " bar");
                string status_result = getBetween(indata, "bar:(", "):");
                string leak_result = getBetween(indata, "): ", " Pa");
                if (leak_result.Contains("-"))
                    pa = -1 * (Convert.ToDouble(leak_result) / 100);
                else
                    pa = Convert.ToDouble(leak_result) / 100;

                double bar = Convert.ToDouble(press_result);
                            
                //if ((peakmin <= bar) && (bar <= peakmax) && (leak_minlimit <= pa) && (pa <= leak_maxlimit))
                if (status_result.Equals("OK"))
                {
                    dataGridView1.Rows[0].Cells[1].Value = bar;
                    dataGridView1.Rows[1].Cells[1].Value = pa;
                    dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.Green;
                    dataGridView1.Rows[2].Cells[1].Value = "PASS";
                    final_pass = 1; //0 - FAIL, 1 - PASS, 2 - Network Disconnected, 3 - Network Connected

                    if (checkBox1.Checked)
                    {
                        // Saving data to DATABASE; Device and DeviceTestID
                        int rest = SaveDB(connectdb, numsid, Convert.ToBoolean(final_pass), pcName);
                        int rest1 = SaveDB_History(connectdb, numsid, Convert.ToBoolean(final_pass), pcName);

                        listBox1.Invoke(new EventHandler(delegate
                        {
                            listBox1.Items.Add(serialnumber + " - PASS (SAVED)");
                        }));
                        //MessageBox.Show("PASS", "Info");
                        ShowMyDialogBox(final_pass);
                        if (rest == 1)
                        {                          
                            dataGridView1.Invoke(new EventHandler(delegate
                            {
                                dataGridView1.Rows[0].Cells[1].Value = "0.0";
                                dataGridView1.Rows[1].Cells[1].Value = "0.0";
                                dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.White;
                                dataGridView1.Rows[2].Cells[1].Value = "";
                            }));
                            textBox1.Invoke(new EventHandler(delegate
                            {
                                textBox1.Focus();
                                textBox1.Clear();
                            }));
                            label12.Invoke(new EventHandler(delegate
                            {
                                label12.ForeColor = Color.Black;
                                label12.Text = "SCAN SERIAL NUMBER";
                            }));
                        }
                        else
                        {
                            dataGridView1.Invoke(new EventHandler(delegate
                            {
                                dataGridView1.Rows[0].Cells[1].Value = "0.0";
                                dataGridView1.Rows[1].Cells[1].Value = "0.0";
                                dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.White;
                                dataGridView1.Rows[2].Cells[1].Value = "";
                            }));
                            textBox1.Invoke(new EventHandler(delegate
                            {
                                textBox1.Focus();
                                textBox1.Clear();
                            }));
                            MessageBox.Show("Safe FAILED, Check DB", "Info");
                            label12.Invoke(new EventHandler(delegate
                            {
                                label12.ForeColor = Color.Black;
                                label12.Text = "SCAN SERIAL NUMBER";
                            }));
                        }
                        SaveLog(indata, numsid, Convert.ToBoolean(final_pass));
                    }
                    else
                    {
                        final_pass = 0;
                        listBox1.Invoke(new EventHandler(delegate
                        {
                            listBox1.Items.Add(serialnumber + " - PASS (NOT SAVED)");
                        }));
                        ShowMyDialogBox(final_pass);

                        dataGridView1.Invoke(new EventHandler(delegate
                        {
                            dataGridView1.Rows[0].Cells[1].Value = "0.0";
                            dataGridView1.Rows[1].Cells[1].Value = "0.0";
                            dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.White;
                            dataGridView1.Rows[2].Cells[1].Value = "";
                        }));
                        textBox1.Invoke(new EventHandler(delegate
                        {
                            textBox1.Focus();
                            textBox1.Clear();
                        }));
                        label12.Invoke(new EventHandler(delegate
                        {
                            label12.ForeColor = Color.Black;
                            label12.Text = "SCAN SERIAL NUMBER";
                        }));

                        SaveLog(indata, numsid, Convert.ToBoolean(final_pass));
                        //MessageBox.Show("PASS", "Info");
                        
                    }
                }
                else
                {
                    dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.Red;
                    dataGridView1.Rows[2].Cells[1].Value = "FAIL";
                    final_pass = 0;
                    listBox1.Invoke(new EventHandler(delegate
                    {
                        listBox1.Items.Add(serialnumber + " - FAIL (NOT SAVED)");
                    }));
                
                    SaveLog(indata, numsid, Convert.ToBoolean(final_pass));
                    //MessageBox.Show("FAIL", "Info");
                    ShowMyDialogBox(final_pass);
                    dataGridView1.Invoke(new EventHandler(delegate
                    {
                        dataGridView1.Rows[0].Cells[1].Value = "0.0";
                        dataGridView1.Rows[1].Cells[1].Value = "0.0";
                        dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.White;
                        dataGridView1.Rows[2].Cells[1].Value = "";
                    }));
                    textBox1.Invoke(new EventHandler(delegate
                    {
                        textBox1.Focus();
                        textBox1.Clear();
                    }));
                    label12.Invoke(new EventHandler(delegate
                    {
                        label12.ForeColor = Color.Black;
                        label12.Text = "SCAN SERIAL NUMBER";
                    }));
                }
            }
            else
            {
                //newport.DataReceived -= SerialDataReceivedEventHandler;
                dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.Red;
                dataGridView1.Rows[2].Cells[1].Value = "FAIL";
                final_pass = 0;
                listBox1.Invoke(new EventHandler(delegate
                {
                    listBox1.Items.Add(serialnumber + " - FAIL (NOT SAVED)");
                }));
                ShowMyDialogBox(final_pass);
                dataGridView1.Invoke(new EventHandler(delegate
                {
                    dataGridView1.Rows[0].Cells[1].Value = "0.0";
                    dataGridView1.Rows[1].Cells[1].Value = "0.0";
                    dataGridView1.Rows[2].Cells[1].Style.BackColor = Color.White;
                    dataGridView1.Rows[2].Cells[1].Value = "";
                }));
                textBox1.Invoke(new EventHandler(delegate
                {
                    textBox1.Focus();
                    textBox1.Clear();
                }));
                label12.Invoke(new EventHandler(delegate
                {
                    label12.ForeColor = Color.Black;
                    label12.Text = "SCAN SERIAL NUMBER";
                }));

                SaveLog(indata, numsid, Convert.ToBoolean(final_pass));
                                
            }
            indata = null;
        }
        /// <summary>
        /// Save DATA to Database, Device TABLE
        /// </summary>
        /// <param name="conndb"> connection string to db sql server</param>
        /// <param name="numstrdb">serial number</param>
        /// <param name="bitpass">status change flag for passedleak paramaer</param>
        /// <param name="pc_string"> computer name</param>
        /// <returns></returns>
        private int SaveDB(string conndb, string numstrdb, bool bitpass, string pc_string)
        {
            int rp = 0;
            using (SqlConnection conn = new SqlConnection(conndb))
            {
                //conn.State = ConnectionState.Broken;
                //queryString = String.Format("UPDATE Device SET PassedLeak=@PassedLeak, StationName=@StationName WHERE ExternalUnitID=@ExternalUnitID");
                queryString = String.Format("UPDATE Device SET PassedLeak=@PassedLeak WHERE ExternalUnitID=@ExternalUnitID");
                conn.Open();
                SqlCommand query = new SqlCommand(queryString, conn);

                query.Parameters.AddWithValue("@PassedLeak", bitpass);
                query.Parameters.AddWithValue("@ExternalUnitID", numstrdb);
                //query.Parameters.AddWithValue("@StationName", pc_string);
                //query.Parameters.AddWithValue("PassedPreBond", SqlDbType.Binary).Value = Convert.ToBoolean(bitpass);
                //query.Parameters.AddWithValue("ExternalUnitID", SqlDbType.VarChar).Value = serialnumber;

                rp = query.ExecuteNonQuery();
            }

            return rp;
        }

        //Save DATA to Database, DeviceTestHistoryID TABLE
        private int SaveDB_History(string conndb, string numstrdb, bool bitpass, string pc_string)
        {
            int rp = 0;
            string sqlres = null;

            if (bitpass)
                sqlres = "PASSED";
            else
                sqlres = "FAILED";

            using (SqlConnection conn = new SqlConnection(conndb))
            {
                //sql string Manufacturer 1 - IONICS, 2 - PM
                if (manufacturer == 1)
                {
                    queryString = String.Format("INSERT INTO DeviceTestHistory (HwTypeRecID, HardwareID, TestDate, TestOperator, TestApp, TestType, TestResults, TopLevel_PNCFG, UserCreated, DateCreated, IsDeleted, UserUpdated, DateUpdated, IsModified, TestStation, RMA, SentToQL)"

                   + " VALUES (@HwTypeRecID, @HardwareID, @TestDate, @TestOperator, @TestApp, @TestType, @TestResults, @TopLevel_PNCFG, @UserCreated, @DateCreated, @IsDeleted, @UserUpdated, @DateUpdated, @IsModified, @TestStation, @RMA, @SentToQL)");
                }
                else
                {
                    queryString = String.Format("INSERT INTO DeviceTestHistory (HwTypeRecID, HardwareID, TestDate, TestOperator, TestApp, TestType, TestResults, TopLevel_PNCFG, UserCreated, DateCreated, IsDeleted, UserUpdated, DateUpdated, IsModified, RMA, SentToQL)"

                                       + " VALUES (@HwTypeRecID, @HardwareID, @TestDate, @TestOperator, @TestApp, @TestType, @TestResults, @TopLevel_PNCFG, @UserCreated, @DateCreated, @IsDeleted, @UserUpdated, @DateUpdated, @IsModified, @RMA, @SentToQL)");
                }

                conn.Open();
                SqlCommand query = new SqlCommand(queryString, conn);

                query.Parameters.AddWithValue("@HwTypeRecID", 29);
                query.Parameters.AddWithValue("@HardwareID", numstrdb);
                query.Parameters.AddWithValue("@TestDate", DateTime.Now);
                query.Parameters.AddWithValue("@TestOperator", username);
                query.Parameters.AddWithValue("@TestApp", fileversion_str);
                query.Parameters.AddWithValue("@TestType", 10); //TestType 10 - is Leak Test code
                query.Parameters.AddWithValue("@TestResults", sqlres);
                //query.Parameters.AddWithValue("@SerialNumber", bitpass);
                query.Parameters.AddWithValue("@TopLevel_PNCFG", pncfg);
                //query.Parameters.AddWithValue("@SubAssemblyPN", pc_string);
                query.Parameters.AddWithValue("@UserCreated", "LVAPP");
                query.Parameters.AddWithValue("@DateCreated", DateTime.Now);
                query.Parameters.AddWithValue("@IsDeleted", 0);
                //query.Parameters.AddWithValue("@UserDeleted", bitpass);
                //query.Parameters.AddWithValue("@DateDeleted", numstrdb);
                query.Parameters.AddWithValue("@UserUpdated", "LVAPP");
                query.Parameters.AddWithValue("@DateUpdated", DateTime.Now);
                //query.Parameters.AddWithValue("@RowVersion", bitpass);
                query.Parameters.AddWithValue("@IsModified", 1);
                if(manufacturer == 1)
                    query.Parameters.AddWithValue("@TestStation", pc_string);
                

                //query.Parameters.AddWithValue("@FixtureSN", NULL);
                //query.Parameters.AddWithValue("@WorkOrder", NULL);
                query.Parameters.AddWithValue("@RMA", 0);
                query.Parameters.AddWithValue("@SentToQL", 0);
                rp = query.ExecuteNonQuery();
            }

            return rp;
        }
        // Creating Log file and saving test data
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
            /*
            if (UncheckFlag(connectdb, serialnumber) == 1)
            {
                MessageBox.Show("PassedLeak Unchecked", "Info");
            }
            else
            {
                MessageBox.Show("Error Detected", "Info");
            }*/

            SaveDB_History(connectdb, serialnumber, false, pcName);
        }
        /// <summary>
        /// Saving Log file with test data
        /// </summary>
        /// <param name="logMessage">full data from ATEQ</param>
        /// <param name="txtWriter">object</param>
        /// <param name="sernum">serial number</param>
        /// <param name="flagit">status of the test PASS/FAIL</param>
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
                MessageBox.Show(ex.Message, "Info");
            }
        }

        // Parsing data
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
        /// <summary>
        /// Searching serial number in database
        /// </summary>
        /// <param name="connectstr">connection string to sql server</param>
        /// <param name="numstr">serial number</param>
        /// <param name="leakstate">ateq status of the test</param>
        /// <returns></returns>
        private bool SearchDB(string connectstr, string numstr, out bool leakstate)
        {
            using (SqlConnection conn = new SqlConnection(connectstr))
            {
                string queryString = @"SELECT * FROM Device WHERE ExternalUnitID = @ExternalUnitID";
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
                    leakstate = PassedLeak;
                    return PassedPreBond;
                }       
            }

        }
        // uncheck function for PassedLeak column value
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
                
                rpep = query.ExecuteNonQuery();
                
            }

            return rpep;
        }
      
    }

}

/*
 * DeviceTestHistoryID],[HwTypeRecID],[HardwareID],[TestDate],[TestOperator],[TestApp],[TestType],[SerialNumber],[TopLevel_PNCFG],[SubAssemblyPN],[UserCreated],[DateCreated],[IsDeleted],[UserDeleted]
      ,[DateDeleted],[UserUpdated],[DateUpdated],[RowVersion],[IsModified],[TestStation],[FixtureSN],[WorkOrder],[RMA],[SentToQL] */


/*
 * 
 * 
    
private void backgroundWorkerComPortPresent_DoWork(object sender, DoWorkEventArgs e)
{
 System.Threading.Thread.Sleep(250);
 BackgroundWorker worker = sender as BackgroundWorker;
}

     public void InfoHandler(object sender, SqlInfoMessageEventArgs e)
        {
            if (OnInfo != null) //check for subscriber
                OnInfo(sender,
                    "Info: " +
                    e.Message);
        }


        public System.Data.SqlClient.SqlConnection Connection;
        protected System.Threading.Thread _bldThrd;

        //this will be the command we execute 
        public System.Data.SqlClient.SqlCommand Command;
        /*public AsyncCmd() { }
        public AsyncCmd(SqlConnection conn, SqlCommand comm)
        {
            this.Connection = conn;
            this.Command = comm;
        }*/
        /*
public delegate void InfoMessage(object sender, string Message);
public event InfoMessage OnInfo;
//bool to show true once our process is complete.
public bool IsComplete = false;
public void ChangeHandler(object sender, StateChangeEventArgs e)
{
    if (OnInfo != null)  //check for subscriber
        OnInfo(sender,
            "SqlConnection Change from " +
            e.OriginalState.ToString() +
            " to " +
            e.CurrentState.ToString());
}
public void ExecSql()
{
    if (OnInfo != null)
        OnInfo(this,
            "AsyncCmd Starting");

    if (this.Connection == null || this.Command == null)
        throw new System.Exception( //fire error if objects not set
            "Both Connection and Command values must be set!");
    if (OnInfo != null)
    { //check for subscriber
        this.Connection.InfoMessage +=  //bubble prints
            new SqlInfoMessageEventHandler(InfoHandler);
        this.Connection.StateChange +=  //bubble open|close
            new StateChangeEventHandler(ChangeHandler);
    }

    this.Connection.Open();
    this.Command.Connection = this.Connection;
    this.Command.CommandTimeout = 0;
    if (OnInfo != null)
        OnInfo(this,
            "Executing SqlCommand");
    this.Command.ExecuteNonQuery();
    this.Connection.Close();
    if (OnInfo != null)
        OnInfo(this,
            "AsyncCmd Complete");
    this.IsComplete = true;
}*/
//start async process
/*
public void Start()
{
    //create new Thread and set ExecSql to the async 
    //method using ThreadStart. 
    _bldThrd = new System.Threading.Thread(
        new System.Threading.ThreadStart(ExecSql));
    _bldThrd.Start();
}

//stop async process, if running
public void Stop()
{
    if (_bldThrd != null) //check Thread init
        if (_bldThrd.IsAlive) //check running
            _bldThrd.Abort(); //kill it
                              //it may have stopped, but wait for it
    this.Join();
}

public void Join()
{
    if (_bldThrd != null) //check Thread init
        if (_bldThrd.IsAlive) //check running
            _bldThrd.Join(); //join it
}
*/
/*
private void backgroundWorkerComPortPresent_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
{
 bool emptyString = string.IsNullOrEmpty(portComboBox.Text);  
 if (e.Error != null)
 MessageBox.Show(e.Error.Message);
 else if (!emptyString)
 {
 bool portOpen = com.comPort.IsOpen;

 if (portOpen)
 {
  portStatusLabel.Text = "The Port Is Open";
 }
 if (!portOpen)
 {
  portStatusLabel.Text = "The Port Is Unavailable";
  if (backgroundWatcherEnabled)
  {
  com.ClosePort();
  backgroundWatcherEnabled = false;
  }
 }
 }
 


    using System.Net;
using System.Net.Sockets;

public bool CheckServerAvailablity(string serverIPAddress, int port)
{
  try
  {
    IPHostEntry ipHostEntry = Dns.Resolve(serverIPAddress);
    IPAddress ipAddress = ipHostEntry.AddressList[0];

    TcpClient TcpClient = new TcpClient();
    TcpClient.Connect(ipAddress , port);
    TcpClient.Close();

    return true;
  }
  catch
  {
    return false;
  }
} 

private delegate void DisplayDialogCallback();

public void DisplayDialog()
{
    if (this.InvokeRequired)
    {
        this.Invoke(new DisplayDialogCallback(DisplayDialog));
    }
    else
    {
        if (this.Handle != (IntPtr)0) // you can also use: this.IsHandleCreated
        {
            this.ShowDialog();

            if (this.CanFocus)
            {
                this.Focus();
            }
        }
        else
        {
            // Handle the error
        }
    }
}

    BeginInvoke( new Action( () =>
    {
        var form = new Form2();
        form.Owner = this;

        form.ShowDialog( this );
    } ) );
}*/

