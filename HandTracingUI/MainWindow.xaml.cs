using JiebaNet.Segmenter.PosSeg;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HandTracingUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        #region 定义变量
        private static int canvasNum = 6;
        private static double OPACITY_DOWN_FACTOR = 0.2;    // 图像之间的透明度差
        private static double SPRINESS = 0.01;		    // 弹性运动参数,原始:0.05
        private static double CRITICAL_POINT = 0.001;
        private static double MOVE_DISTANCE = 100; //移动距离后转换,原始:35

        private int waitingNum = 0;
        private int welcomeAnimeFadingInTime = 1;//秒
        private int welcomeAnimeFadingOutTime = 1;
        private int entryAnimeFadingInTime = 1;
        private int patientPIDHoverTime = 15; //毫秒
        private int patientOrganHoverTimer = 2; //秒
        private int gapDistance = 200;

        private double _touch_move_distance = 3;//原始:0
        private double _target = 0;		// 目标位置
        private double _current = 0;    // 当前位置
        private double _xCenter;
        private double _yCenter;
        private double _beishu = 0.2; // 移动距离占屏幕宽度的倍数

        private string startPath = System.Environment.CurrentDirectory;
        private string strConnection = "Provider=Microsoft.Jet.OleDb.4.0;Data Source=userdata.mdb";
        private string _sql;
        private string _keshiFlag = "0"; // 科室标志位
        private string _specificKeshiFlag = "1"; // 具体科室标志位
        private string _organFlag = "2"; // 器官标志位
        private string _patientNameFlag = "3"; // 病人标志位
        private string _orderFlag = "4"; // 命令标志位
        private string _globalKeshiType = "";
        private string _globalKeshiID = "";
        private string _globalPatient = "Nobody";
        private string _globalOrganName = "";
        private string _globalOrganImageNum = "0";
        private string _globalCommandStr = "";
        private string _cutStr;
        private string serverIP = "127.0.0.1";
        private string[] _arrayCutStr;
        private string[] IMAGES;

        private DispatcherTimer neike_timer = new DispatcherTimer();
        private DispatcherTimer neikeSmooth_timer = new DispatcherTimer();
        private DispatcherTimer waike_timer = new DispatcherTimer();
        private DispatcherTimer pifuke_timer = new DispatcherTimer();
        private DispatcherTimer patientOrgan_timer = new DispatcherTimer();

        private bool haveKeyValue = false;

        private Canvas[] arrayCanvas = new Canvas[canvasNum];
        private List<Viewport3DControl> _images = new List<Viewport3DControl>();
        private DirectoryInfo organ_folder;//
        private Dictionary<string, string> voiceStrDic = new Dictionary<string, string>();
        #endregion

        #region MainWindow()实例
        public MainWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region 设置全屏 LOAD
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConsoleManager.Show();//打开控制台窗口  
            // 设置全屏    
            
            this.WindowState = System.Windows.WindowState.Normal;
            this.WindowStyle = System.Windows.WindowStyle.None;
            this.ResizeMode = System.Windows.ResizeMode.NoResize;
            
            this.Topmost = true;

            this.Left = 0.0;
            this.Top = 0.0;
            this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;
            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;

            // 设置
            setPatientOrganGroup("NoVoice");

            // 鼠标指针
            //this.Cursor = new Cursor(startPath + "\\conf\\Cursor\\hand.cur");

            // 初始化图层
            initCanvas();

            // 设置welcome图像，科室
            welcomeInfo.Source = getBitmap(startPath + "\\conf\\Image\\welcome.png");
            entery_neike_img.Source = getBitmap(startPath + "\\conf\\Image\\neike.png");
            entery_waike_img.Source = getBitmap(startPath + "\\conf\\Image\\waike.png");
            entery_pifuke_img.Source = getBitmap(startPath + "\\conf\\Image\\pifuke.png");

            // 焦点放在语音输入框上
            input_tb.Focus();

            // 初始化并进入welcome
            initWelcome();
            showCanvas(welcome_canvas);

            //
            input_tb.Foreground = Brushes.White;
            input_lb.Foreground = Brushes.White;
        }
        #endregion

        #region 初始化图层
        private void initCanvas()
        {
            arrayCanvas[0] = welcome_canvas;
            arrayCanvas[1] = entry_canvas;
            arrayCanvas[2] = patientInDepart_canvas;
            arrayCanvas[3] = specificOrganImageGroup_canvas;
            arrayCanvas[4] = specificOrganImage_canvas;
            arrayCanvas[5] = justOnePatient_canvas;
        }
        #endregion

        #region 初始化欢迎界面
        private void initWelcome()
        {
            // showCanvas(4);
            DoubleAnimation welcomeFadingIn_anime = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(welcomeAnimeFadingInTime)));
            welcomeFadingIn_anime.Completed += WelcomeFadingIn_anime_Completed;
            welcomeInfo.BeginAnimation(UIElement.OpacityProperty, welcomeFadingIn_anime);
        }

        private void WelcomeFadingIn_anime_Completed(object sender, EventArgs e)
        {
            DoubleAnimation welcomeFadingOut_anime = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(welcomeAnimeFadingOutTime)));
            welcomeFadingOut_anime.Completed += WelcomeFadingOut_anime_Completed;
            welcomeInfo.BeginAnimation(UIElement.OpacityProperty, welcomeFadingOut_anime);
        }

        private void WelcomeFadingOut_anime_Completed(object sender, EventArgs e)
        {
            showCanvas(entry_canvas);
        }
        #endregion

        #region 更新某个科室下病人信息
        private void UpdatePatientInDepartInfo()
        {
            DoubleAnimation patientInDepart_anime = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(entryAnimeFadingInTime)));
            patientInDepart_canvas.BeginAnimation(UIElement.OpacityProperty, patientInDepart_anime);
            patients_lb_PIDcanvas.Items.Clear();
            Console.WriteLine("_globalKeshiType:" + _globalKeshiType);
            try
            {
                _sql = "select * from alldata, userInfo, keshi where alldata.人员ID=userInfo.人员ID and alldata.科室ID=keshi.科室ID and 科室='" + _globalKeshiType + "'";
                OleDbConnection objConnection = new OleDbConnection(strConnection);  //建立连接  
                objConnection.Open();  //打开连接  
                OleDbCommand sqlcmd = new OleDbCommand(@_sql, objConnection);  //sql语句  
                OleDbDataReader reader = sqlcmd.ExecuteReader();              //执行查询  
                while (reader.Read())
                {
                    Button patients_btn_PIDcanvas = new Button();
                    //patients_btn_PIDcanvas.SetValue(Button.StyleProperty, Application.Current.Resources["MaterialDesignFloatingActionMiniButton"]);
                    ImageBrush brush = new ImageBrush();
                    brush.ImageSource = new BitmapImage(new Uri(startPath + "\\conf\\Image\\btnbg1.png", UriKind.Relative));
                    patients_btn_PIDcanvas.Background = brush;
                    patients_btn_PIDcanvas.Foreground = Brushes.Black;
                    patients_btn_PIDcanvas.Height = 40;
                    patients_btn_PIDcanvas.Width = 60;
                    patients_btn_PIDcanvas.Content = reader["alldata.人员名称"].ToString();
                    patients_btn_PIDcanvas.FontSize = 10;
                    patients_btn_PIDcanvas.Name = "病人ID" + reader["alldata.人员ID"].ToString();
                    patients_btn_PIDcanvas.Click += Patients_btn_PIDcanvas_Click;
                    patients_btn_PIDcanvas.MouseEnter += Patients_btn_PIDcanvas_MouseEnter;
                    patients_lb_PIDcanvas.Items.Add(patients_btn_PIDcanvas);
                }
                objConnection.Close();
                reader.Close();
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 具体科室下病人悬停MouseEnter
        private void Patients_btn_PIDcanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            _globalPatient = ((Button)sender).Name.ToString();
            
            Console.WriteLine("MouseEnter" + _globalPatient);
            waitingNum = 0;
            this.Cursor = new Cursor(startPath + "\\conf\\Cursor\\hand.cur");
            fetchPatientInfo("NoVoice");
        }
        #endregion

        #region 具体科室下单击病人执行事件
        private void Patients_btn_PIDcanvas_Click(object sender, RoutedEventArgs e)
        {
            _globalPatient = ((Button)sender).Name.ToString();
            fetchPatientInfo("NoVoice");
        }

        private void fetchPatientInfo(string patten)
        {
            //sentData(_globalPatient);
            if(patten == "NoVoice")
            {
                if(_globalPatient == "Nobody")
                {
                    // do nothing...
                }
                else
                {
                    _globalPatient = _globalPatient.Replace("病人ID","");
                    
                    patientsOrgan_lb_PIDcanvas.Items.Clear();
                    try
                    {
                        _sql = "select * from userInfo,alldata,keshi,organ where userInfo.人员ID=alldata.人员ID and alldata.器官ID=organ.器官ID and keshi.科室ID=alldata.科室ID and keshi.科室='" + _globalKeshiType + "' and userInfo.人员ID='" + _globalPatient + "'";
                        OleDbConnection objConnection = new OleDbConnection(strConnection);  //建立连接  
                        objConnection.Open();  //打开连接  
                        OleDbCommand sqlcmd = new OleDbCommand(@_sql, objConnection);  //sql语句  
                        OleDbDataReader reader = sqlcmd.ExecuteReader();              //执行查询  
                        while (reader.Read())
                        {
                            Button patientsOrgan_btn_PIDcanvas = new Button();
                            //patientsOrgan_btn_PIDcanvas.SetValue(Button.StyleProperty, Application.Current.Resources["MaterialDesignFloatingActionMiniButton"]);
                            ImageBrush brush = new ImageBrush();
                            brush.ImageSource = new BitmapImage(new Uri(startPath + "\\conf\\Image\\btnbg1.png", UriKind.Relative));
                            patientsOrgan_btn_PIDcanvas.Background = brush;
                            patientsOrgan_btn_PIDcanvas.Foreground = Brushes.Black;
                            patientsOrgan_btn_PIDcanvas.Height = 60;
                            patientsOrgan_btn_PIDcanvas.Width = 60;
                            patientsOrgan_btn_PIDcanvas.Content = reader["器官名称"].ToString();
                            patientsOrgan_btn_PIDcanvas.Name = "器官名称" + reader["器官名称"].ToString();
                            patientsOrgan_lb_PIDcanvas.Items.Add(patientsOrgan_btn_PIDcanvas);
                            patientsOrgan_btn_PIDcanvas.MouseEnter += patientsOrgan_btn_PIDcanvas_MouseEnter;
                            patientsOrgan_btn_PIDcanvas.MouseLeave += patientsOrgan_btn_PIDcanvas_MouseLeave;
                            patientPortrait_img_PIDcanvas.Source = getBitmap(startPath + "\\conf\\PatientsPortrait\\" + reader["头像路径"].ToString());
                            patientPortrait_img_PIDcanvas.Stretch = Stretch.Fill;
                            patientInfo_tb_PIDcanvas.Text = "ID：" + reader["userInfo.人员ID"].ToString() + "\n" +
                                                            "姓名：" + reader["userInfo.人员名称"].ToString() + "\n" +
                                                            "性别：" + reader["性别"].ToString();
                            tb_bingli.Text = "";
                            tb_bingli.Text += "病历：\n" +reader["病历"].ToString();
                            patientInfo_tb_PIDcanvas.Foreground = Brushes.White;
                        }
                        objConnection.Close();
                        reader.Close();
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine(ex.ToString());
                    }
                }
            }
            else if(patten == "Voice")
            {
                Console.WriteLine("voice");
                patientsOrgan_lb_PIDcanvas.Items.Clear();
                patients_lb_PIDcanvas.Items.Clear();
                bool showFirstPatient = true;
                try
                {
                    _sql = "select * from userInfo,alldata,keshi,organ where userInfo.人员ID=alldata.人员ID and alldata.器官ID=organ.器官ID and keshi.科室ID=alldata.科室ID and keshi.科室='" + _globalKeshiType + "' and userInfo.人员名称='" + _globalPatient + "'";
                    //Console.WriteLine("voice sql" + _sql);
                    OleDbConnection objConnection = new OleDbConnection(strConnection);  //建立连接  
                    objConnection.Open();  //打开连接  
                    OleDbCommand sqlcmd = new OleDbCommand(@_sql, objConnection);  //sql语句  
                    OleDbDataReader reader = sqlcmd.ExecuteReader();              //执行查询  
                    bool ishave = false;
                    while (reader.Read())
                    {
                        Button patients_btn_PIDcanvas = new Button();
                        //patients_btn_PIDcanvas.SetValue(Button.StyleProperty, Application.Current.Resources["MaterialDesignFloatingActionMiniButton"]);
                        ImageBrush brush = new ImageBrush();
                        brush.ImageSource = new BitmapImage(new Uri(startPath + "\\conf\\Image\\btnbg1.png", UriKind.Relative));
                        patients_btn_PIDcanvas.Background = brush;
                        patients_btn_PIDcanvas.Foreground = Brushes.Black;
                        patients_btn_PIDcanvas.Height = 40;
                        patients_btn_PIDcanvas.Width = 60;
                        patients_btn_PIDcanvas.FontSize = 10;
                        patients_btn_PIDcanvas.Content = reader["alldata.人员名称"].ToString();
                        patients_btn_PIDcanvas.Name = "病人ID" + reader["alldata.人员ID"].ToString();
                        patients_btn_PIDcanvas.Click += Patients_btn_PIDcanvas_Click;
                        patients_btn_PIDcanvas.MouseEnter += Patients_btn_PIDcanvas_MouseEnter;
                        if (!ishave)
                        {
                            patients_lb_PIDcanvas.Items.Add(patients_btn_PIDcanvas);
                            ishave = true;
                        }
                        if (showFirstPatient)
                        {
                            Button patientsOrgan_btn_PIDcanvas = new Button();
                            //patientsOrgan_btn_PIDcanvas.SetValue(Button.StyleProperty, Application.Current.Resources["MaterialDesignFloatingActionMiniButton"]);
                            brush = new ImageBrush();
                            brush.ImageSource = new BitmapImage(new Uri(startPath + "\\conf\\Image\\btnbg1.png", UriKind.Relative));
                            patientsOrgan_btn_PIDcanvas.Background = brush;
                            patientsOrgan_btn_PIDcanvas.Foreground = Brushes.Black;
                            patientsOrgan_btn_PIDcanvas.Height = 60;
                            patientsOrgan_btn_PIDcanvas.Width = 60;
                            patientsOrgan_btn_PIDcanvas.Content = reader["器官名称"].ToString();
                            patientsOrgan_btn_PIDcanvas.Name = "器官名称" + reader["器官名称"].ToString();
                            patientsOrgan_btn_PIDcanvas.Margin = new Thickness(0,0,0,0);
                            patientsOrgan_lb_PIDcanvas.Items.Add(patientsOrgan_btn_PIDcanvas);



                            patientsOrgan_btn_PIDcanvas.MouseEnter += patientsOrgan_btn_PIDcanvas_MouseEnter;
                            patientsOrgan_btn_PIDcanvas.MouseLeave += patientsOrgan_btn_PIDcanvas_MouseLeave;
                            patientPortrait_img_PIDcanvas.Source = getBitmap(startPath + "\\conf\\PatientsPortrait\\" + reader["头像路径"].ToString());
                            patientInfo_tb_PIDcanvas.Text = "ID：" + reader["userInfo.人员ID"].ToString() + "\n" +
                                                            "姓名：" + reader["userInfo.人员名称"].ToString() + "\n" +
                                                            "性别：" + reader["性别"].ToString();
                            //showFirstPatient = false;
                            tb_bingli.Text = "";
                            tb_bingli.Text += "病历：\n" + reader["病历"].ToString();
                            patientInfo_tb_PIDcanvas.Foreground = Brushes.White;
                        }
                    }
                    showFirstPatient = false;
                    objConnection.Close();
                    reader.Close();
                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ex.ToString());
                }
            }
        }

        private void patientsOrgan_btn_PIDcanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            patientOrgan_timer.Stop();
        }

        private void patientsOrgan_btn_PIDcanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                _globalOrganName = ((Button)sender).Name.ToString();
                Console.WriteLine("_globalOrganName:   " + _globalOrganName);
                patientOrgan_timer.Interval = new TimeSpan(0, 0, 2);
                patientOrgan_timer.Tick += PatientOrgan_timer_Tick;
                patientOrgan_timer.Start();
            }
            catch(Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }

        private void PatientOrgan_timer_Tick(object sender, EventArgs e)
        {
            try
            {
                patientOrgan_timer.Stop();
                setPatientOrganGroup("NoVoice");
                showCanvas(specificOrganImageGroup_canvas);
            }
            catch(Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 显示不同的图层
        private void showCanvas(Canvas canvasName)
        {
            for(int i = 0; i < canvasNum; ++i)
            {
                if(arrayCanvas[i] == canvasName)
                {
                    arrayCanvas[i].Visibility = Visibility.Visible;
                }
                else
                {
                    arrayCanvas[i].Visibility = Visibility.Hidden;
                }
            }
            if(canvasName == entry_canvas)
            {
                _globalPatient = "Nobody";
                DoubleAnimation entryCanvas_anime = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(entryAnimeFadingInTime)));
                entry_canvas.BeginAnimation(UIElement.OpacityProperty, entryCanvas_anime);
            }
            else if(canvasName == patientInDepart_canvas)
            {
                UpdatePatientInDepartInfo();
            }
            else if (canvasName == specificOrganImage_canvas) // 初始化图像的位置
            {
                Console.WriteLine("初始化图像的位置");
                /*
                specificOrganImage_img_SOIcanvas.Height = LayoutRoot.ActualHeight;
                double temp_Width = specificOrganImage_img_SOIcanvas.Width;
                
                
                Matrix m = specificOrganImage_img_SOIcanvas.RenderTransform.Value;
                Console.WriteLine(LayoutRoot.Width);
                Console.WriteLine(LayoutRoot.Height);
                Console.WriteLine(specificOrganImage_img_SOIcanvas.Width);
                Console.WriteLine(specificOrganImage_img_SOIcanvas.Height);
                m.OffsetX = (LayoutRoot.ActualWidth - specificOrganImage_img_SOIcanvas.Width * LayoutRoot.ActualHeight / specificOrganImage_img_SOIcanvas.Height) / 2;
                m.OffsetY = 0;
                specificOrganImage_img_SOIcanvas.RenderTransform = new MatrixTransform(m);
                */
            }
        }
        #endregion

        #region 内科控件悬停MouseEnter
        private void entery_neike_img_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = new Cursor(startPath + "\\conf\\Cursor\\hand.cur");
            neike_timer.Interval = TimeSpan.FromMilliseconds(15);
            neike_timer.Tick += Neike_timer_Tick;
            waitingNum = 0;
            neike_timer.Start();
        }

        private void Neike_timer_Tick(object sender, EventArgs e)
        {
            if(waitingNum != 105)
            {
                if(waitingNum > 100)
                    entery_neike_epb.Value = 100;
                else
                    entery_neike_epb.Value = waitingNum;
                waitingNum += 1;
            }
            else
            {
                neike_timer.Stop();
                waitingNum = 0;
                _globalKeshiType = "内科";
                showCanvas(patientInDepart_canvas);
            }
        }
        #endregion

        #region 内科控件悬停MouseLeave
        private void entery_neike_img_MouseLeave(object sender, MouseEventArgs e)
        {
            neike_timer.Stop();
            waitingNum = 0;
            entery_neike_epb.Value = 0;
        }
        #endregion

        #region 外科控件悬停MouseEnter
        private void entery_waike_img_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = new Cursor(startPath + "\\conf\\Cursor\\hand.cur");
            waike_timer.Interval = TimeSpan.FromMilliseconds(15);
            waike_timer.Tick += Waike_timer_Tick;
            waitingNum = 0;
            waike_timer.Start();
        }

        private void Waike_timer_Tick(object sender, EventArgs e)
        {
            if (waitingNum != 105)
            {
                if (waitingNum > 100)
                    entery_waike_epb.Value = 100;
                else
                    entery_waike_epb.Value = waitingNum;
                waitingNum += 1;
            }
            else
            {
                waike_timer.Stop();
                waitingNum = 0;
                _globalKeshiType = "外科";
                showCanvas(patientInDepart_canvas);
            }
        }
        #endregion

        #region 内科控件悬停MouseLeave
        private void entery_waike_img_MouseLeave(object sender, MouseEventArgs e)
        {
            waike_timer.Stop();
            waitingNum = 0;
            entery_waike_epb.Value = 0;
        }
        #endregion

        #region 皮肤科控件悬停MouseEnter
        private void entery_pifuke_img_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = new Cursor(startPath + "\\conf\\Cursor\\hand.cur");
            pifuke_timer.Interval = TimeSpan.FromMilliseconds(15);
            pifuke_timer.Tick += Pifuke_timer_Tick;
            waitingNum = 0;
            pifuke_timer.Start();
        }

        private void Pifuke_timer_Tick(object sender, EventArgs e)
        {
            if (waitingNum != 105)
            {
                if (waitingNum > 100)
                    entery_pifuke_epb.Value = 100;
                else
                    entery_pifuke_epb.Value = waitingNum;
                waitingNum += 1;
            }
            else
            {
                pifuke_timer.Stop();
                waitingNum = 0;
                _globalKeshiType = "皮肤科";
                showCanvas(patientInDepart_canvas);
            }
        }
        #endregion

        #region 皮肤科控件悬停MouseLeave
        private void entery_pifuke_img_MouseLeave(object sender, MouseEventArgs e)
        {
            pifuke_timer.Stop();
            waitingNum = 0;
            entery_pifuke_epb.Value = 0;
        }
        #endregion

        #region 获取图片控件的source
        private BitmapImage getBitmap(string path)
        {
            BitmapImage bmp = new BitmapImage();
            // BitmapImage.UriSource must be in a BeginInit/EndInit block.  
            bmp.BeginInit();
            string ppath = path;
            bmp.UriSource = new Uri(@ppath, UriKind.RelativeOrAbsolute);
            bmp.EndInit();
            return bmp;
        }
        #endregion

        #region 语音输入框文本变化时的事件
        private void input_tb_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (input_tb.Text.ToString() == "")
            {
                // do nothing...
            }
            else
            {
                Console.WriteLine(DateTime.Now + ": 【语音字符串】" + dealInputString(input_tb.Text.ToString()));
                goDealVoiceString(dealInputString(input_tb.Text.ToString()));
                /*
                try
                {
                    _sql = "select * from confData";
                    OleDbConnection objConnection = new OleDbConnection(strConnection);  //建立连接  
                    objConnection.Open();  //打开连接  
                    OleDbCommand sqlcmd = new OleDbCommand(@_sql, objConnection);  //sql语句  
                    OleDbDataReader reader = sqlcmd.ExecuteReader();              //执行查询  
                    while (reader.Read())
                    {
                        if (reader["关键字"].ToString() == dealInputString(input_tb.Text.ToString()))
                        {
                            Console.WriteLine(dealInputString(input_tb.Text.ToString()));
                            input_lb.Content = dealInputString(input_tb.Text.ToString());
                            haveKeyValue = true;
                            //input_tb.Text = "";
                            // GO ACTION
                            if (reader["类型"].ToString() == _keshiFlag) // 科室：显示不同的科室
                            {
                                showCanvas(entry_canvas);
                            }
                            else if (reader["类型"].ToString() == _specificKeshiFlag) // 具体科室，如【内科】下的病人列表
                            {
                                showCanvas(patientInDepart_canvas);
                                _globalKeshiType = reader["关键字"].ToString();
                                _globalKeshiID = reader["类型"].ToString();
                                UpdatePatientInDepartInfo();
                            }
                            else if (reader["类型"].ToString() == _patientNameFlag) // 病人标志位
                            {
                                if (patientInDepart_canvas.Visibility == Visibility.Visible)
                                {
                                    _globalPatient = reader["关键字"].ToString();
                                    Console.WriteLine("voice"+ _globalPatient);
                                    fetchPatientInfo("Voice");
                                }
                            }
                            else if(reader["类型"].ToString() == _organFlag)
                            {
                                if(patientInDepart_canvas.Visibility == Visibility.Visible)
                                {
                                    _globalOrganName = reader["关键字"].ToString();
                                    setPatientOrganGroup("Voice");
                                    showCanvas(specificOrganImageGroup_canvas);
                                }
                            }
                            else if (reader["类型"].ToString() == _orderFlag) // 命令
                            {
                                if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                                {
                                    switch (reader["关键字"].ToString())
                                    {
                                        case "放大":
                                            GoBig();
                                            break;
                                        case "缩小":
                                            GoSmall();
                                            break;
                                        case "上移":
                                            GoUp();
                                            break;
                                        case "下移":
                                            GoDown();
                                            break;
                                        case "左移":
                                            GoLeft();
                                            break;
                                        case "右移":
                                            GoRight();
                                            break;
                                        case "上一张":
                                            GoPre();
                                            break;
                                        case "下一张":
                                            Console.WriteLine("alsdfsaf");
                                            GoNext();
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                else if( specificOrganImageGroup_canvas.Visibility == Visibility.Visible)
                                {
                                    switch (reader["关键字"].ToString())
                                    {
                                        case "返回":
                                            GoBack();
                                            break;
                                        case "上一张":
                                            GoPre();
                                            break;
                                        case "下一张":
                                            GoNext();
                                            break;
                                        case "确定":
                                            GoConfirm();
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                if( reader["关键字"].ToString() == "返回" )
                                {
                                    GoBack();
                                }
                            }
                        }
                    }
                    input_lb.Content = dealInputString(input_tb.Text.ToString()) + (haveKeyValue ? "" : "(无)");
                    input_tb.Text = "";
                    haveKeyValue = false;
                    objConnection.Close();
                    reader.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                 
                 */

            }
        }
        #endregion

        #region 处理语音输入字符串
        private String dealInputString(string str)
        {
            string outStr = "";
            outStr = str.Replace(" ", "").Replace("，", "").Replace(",", "").Replace("\n", "");
            return outStr;
        }
        #endregion

        #region 器官图片组配置文件
        private static double childViewWidth = 300;
        public double ChildViewWidth
        {
            get { return childViewWidth; }
            set { childViewWidth = value; }
        }

        private double childViewHeight = 350;//原始:300

        public double ChildViewHeight
        {
            get { return childViewHeight; }
            set { childViewHeight = value; }
        }
        private double spaceWidth = 150; //原始:100

        public double SpaceWidth
        {
            get { return spaceWidth; }
            set { spaceWidth = value; }
        }
        #endregion

        #region 设置器官图片组
        private void setPatientOrganGroup(string patten)
        {
            imageGroupInSOIG_canvas.Children.Clear();
            _target = 0;
            _current = 0;
            try
            {
                if(patten == "NoVoice")
                {
                    organ_folder = new DirectoryInfo(startPath + "\\patientdata\\" + _globalOrganName.Replace("器官名称",""));
                }
                else if(patten == "Voice")
                {
                    organ_folder = new DirectoryInfo(startPath + "\\patientdata\\" + _globalOrganName.Replace("器官名称", ""));
                }
                _images.Clear();

                int curimageNum = 0;
                foreach (FileInfo file in organ_folder.GetFiles("*.jpg"))
                {
                    curimageNum += 1;
                }
                _globalOrganImageNum = curimageNum.ToString();
                diNzhang_SOIG.Content = "第" + (_current+1) + "张/共" + _globalOrganImageNum + "张";
                IMAGES = new string[curimageNum];
                curimageNum = 0;
                foreach (FileInfo file in organ_folder.GetFiles("*.jpg"))
                {
                    Console.WriteLine(file.FullName);
                    IMAGES[curimageNum] = file.FullName;
                    curimageNum += 1;
                }
                AddImages(IMAGES);
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 添加器官图片
        public void AddImages(string[] imagesUri)
        {
            for (int i = 0; i < imagesUri.Length; i++)
            {
                string url = imagesUri[i];
                Viewport3DControl image = new Viewport3DControl();
                image.SetImageSource(url);
                image.Index = i;
                image.Width = specificOrganImageGroup_canvas.ActualHeight / 2;
                image.Height = specificOrganImageGroup_canvas.ActualHeight / 2;
                imageGroupInSOIG_canvas.Children.Add(image);
                setImage(image, i);
                _images.Add(image);
            }
        }
        #endregion

        #region 单击或者双击器官图片组，查看上一张或者下一张
        private void imageGroupInSOIG_canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            /*
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_target >= 0)
                    move(-1);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                if (_target <= _images.Count)
                    move(1);
            }
            */
        }

        private void move(int value)
        {
            _target = _current + value;
            for (int i = 0; i < _images.Count; i++)
            {
                Viewport3DControl image = _images[i];
                setImage(image, i);
            }
            _current = _target;
            diNzhang_SOIG.Content = "第" + (_current+1) + "张/共" + _globalOrganImageNum + "张";
        }

        private void setImage(Viewport3DControl image, int index)
        {
            image.SetValue(Canvas.LeftProperty, (specificOrganImageGroup_canvas.ActualWidth - image.Width + (index - _target) * gapDistance) / 2);
            image.SetValue(Canvas.TopProperty, (specificOrganImageGroup_canvas.ActualHeight - image.Height) / 2);
            image.SetValue(Canvas.ZIndexProperty, (int)(-Math.Abs(index - _target) * 100)); // 前后层显示
            image.Opacity = 1 - Math.Abs(index - _target) * OPACITY_DOWN_FACTOR; // 透明度显示
            if (index == _target)
                image.AnimationRotateTo(1, 1, 1, 0);
            else
                image.AnimationRotateTo(0.9, 0.9, 1, 45 * (index > _target? -1:1));
        }
        #endregion

        #region 组合键事件
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // 图片放大 
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                Console.WriteLine(DateTime.Now + ": 【手势操作】放大");
                GoBig();
            }

            // 图片缩小 
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.G)
            {
                Console.WriteLine(DateTime.Now + ": 【手势操作】缩小");
                GoSmall();
            }

            // 图片上移 
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.L)
            {
                Console.WriteLine(DateTime.Now + ": 【手势操作】向上移动");
                GoUp();
            }

            // 图片下移。在图片组中的功能为确定，在单张具体的图层中是下移
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.K)
            {
                GoDown();
                GoConfirm();
            }

            // 图片左移。在图片组中是整体向左移动，在单张图片中是图片向左移动
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                GoLeft();
                GoNext();
            }

            // 图片右移
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.O)
            {
                GoRight();
                GoPre();
            }

            // 返回键
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.T)
            {
                GoBack();
            }
        }
        #endregion

        #region 处理语音字符串
        private void goDealVoiceString(string voiceStr)
        {
            voiceStr = chineseNumberToNumber(voiceStr);
            var posSeg = new PosSegmenter();
            var tokens = posSeg.Cut(voiceStr);
            _cutStr = string.Join(" ", tokens.Select(token => string.Format("{0}/{1}", token.Word, token.Flag)));
            _arrayCutStr = _cutStr.Split(' ');
            //Console.WriteLine(_cutStr);
            Console.WriteLine(DateTime.Now + ": 【语音分割完成】" + _cutStr);
            try
            {
                voiceStrDic.Clear();
                // 将关键字存入字典,关键字有mypersonname,mykeshiname,mycommand,myorganname,myallkeshi,mynumber,m
                for (int i = 0; i < _arrayCutStr.Length; ++i)
                {
                    if(_arrayCutStr[i].Contains('/') && (_arrayCutStr[i].Split('/')[1].ToString().Trim() == "mypersonname" ||
                        _arrayCutStr[i].Split('/')[1].ToString().Trim() == "mykeshiname" ||
                        _arrayCutStr[i].Split('/')[1].ToString().Trim() == "mycommand" ||
                        _arrayCutStr[i].Split('/')[1].ToString().Trim() == "myorganname" ||
                        _arrayCutStr[i].Split('/')[1].ToString().Trim() == "myallkeshi" ||
                        _arrayCutStr[i].Split('/')[1].ToString().Trim() == "mynumber"
                        ))
                        voiceStrDic.Add( _arrayCutStr[i].Split('/')[1].ToString(), _arrayCutStr[i].Split('/')[0].ToString());
                    if (_arrayCutStr[i].Split('/')[1].ToString().Trim() == "m" )
                    {
                        if (voiceStrDic.ContainsKey("m"))
                        {
                            voiceStrDic["m"] = _arrayCutStr[i].Split('/')[0].ToString();
                        }
                        else
                        {
                            voiceStrDic.Add("m", _arrayCutStr[i].Split('/')[0].ToString());
                        }
                    }
                }
                if (voiceStrDic.ContainsKey("myallkeshi")) // 语音中包含“科室”
                {
                    showCanvas(entry_canvas);
                    haveKeyValue = true;
                }
                else if (voiceStrDic.ContainsKey("mycommand")) // 语音中有命令词
                {
                    haveKeyValue = true;
                    if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                    {
                        switch (voiceStrDic["mycommand"])
                        {
                            case "放大":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】放大");
                                GoBig();
                                break;
                            case "缩小":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】缩小");
                                GoSmall();
                                break;
                            case "上移":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】向上移动");
                                GoUp();
                                break;
                            case "下移":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】向下移动");
                                GoDown();
                                break;
                            case "左移":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】向左移动");
                                GoLeft();
                                break;
                            case "右移":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】向右移动");
                                GoRight();
                                break;
                            case "上一张":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】上一张");
                                GoPre();
                                break;
                            case "下一张":
                                Console.WriteLine(DateTime.Now + ": 【语音图片操作】下一张");
                                //Console.WriteLine("alsdfsaf");
                                GoNext();
                                break;
                            default:
                                break;
                        }
                    }
                    else if (specificOrganImageGroup_canvas.Visibility == Visibility.Visible) 
                    {
                        switch (voiceStrDic["mycommand"])
                        {
                            case "返回":
                                Console.WriteLine(DateTime.Now + ": 【语音操作】返回");
                                GoBack();
                                break;
                            case "上一张":
                                Console.WriteLine(DateTime.Now + ": 【语音操作】上一张");
                                GoPre();
                                break;
                            case "下一张":
                                Console.WriteLine(DateTime.Now + ": 【语音操作】下一张");
                                GoNext();
                                break;
                            case "确定":
                                Console.WriteLine(DateTime.Now + ": 【语音操作】确定");
                                GoConfirm();
                                break;
                            default:
                                break;
                        }
                    }
                    if (voiceStrDic["mycommand"] == "返回")
                    {
                        GoBack();
                    }
                }
                // 语音中只包含器官
                else if (voiceStrDic.ContainsKey("myorganname") && !voiceStrDic.ContainsKey("mypersonname") && !voiceStrDic.ContainsKey("mykeshiname"))
                {
                    if (patientInDepart_canvas.Visibility == Visibility.Visible)
                    {
                        _globalOrganName = voiceStrDic["myorganname"].ToString();
                        setPatientOrganGroup("Voice");
                        showCanvas(specificOrganImageGroup_canvas);
                    }
                }
                // 语音中只包括科室的名字
                else if (!voiceStrDic.ContainsKey("myorganname") && !voiceStrDic.ContainsKey("mypersonname") && voiceStrDic.ContainsKey("mykeshiname"))
                {
                    showCanvas(patientInDepart_canvas);
                    _globalKeshiType = voiceStrDic["mykeshiname"].ToString();
                    UpdatePatientInDepartInfo();
                }
                // 语音中只包括病人的名字
                else if (!voiceStrDic.ContainsKey("myorganname") && voiceStrDic.ContainsKey("mypersonname") && !voiceStrDic.ContainsKey("mykeshiname"))
                {
                    if (patientInDepart_canvas.Visibility == Visibility.Visible)
                    {
                        _globalPatient = voiceStrDic["mypersonname"].ToString();
                        //Console.WriteLine("voice" + _globalPatient);
                        fetchPatientInfo("Voice");
                    }
                }
                // 语音中包括数字，判定为命令
                else if(voiceStrDic.ContainsKey("mynumber"))
                {
                    if (specificOrganImageGroup_canvas.Visibility == Visibility.Visible)
                    {
                        //Console.WriteLine(voiceStrDic["mynumber"]);
                        GoToImage(Convert.ToInt32(voiceStrDic["mynumber"]));
                    }
                }
                // 语音中包括数字，判定为命令
                else if (voiceStrDic.ContainsKey("m"))
                {
                    if (specificOrganImageGroup_canvas.Visibility == Visibility.Visible)
                    {
                        //Console.WriteLine(voiceStrDic["m"]);
                        GoToImage(Convert.ToInt32(voiceStrDic["m"]));
                    }
                }
                // 语音中包括病人名字和器官名字
                else if (voiceStrDic.ContainsKey("myorganname") && voiceStrDic.ContainsKey("mypersonname") && !voiceStrDic.ContainsKey("mykeshiname"))
                {
                    _globalOrganName = voiceStrDic["myorganname"];
                    showCanvas(specificOrganImageGroup_canvas);
                    setPatientOrganGroup("Voice");
                }
                input_lb.Content = dealInputString(input_tb.Text.ToString()) + (haveKeyValue ? "" : "");
                input_tb.Text = "";
                haveKeyValue = false;
            }
            catch (Exception ex)
            {
                voiceStrDic.Clear();
                input_tb.Text = "";
                input_lb.Content = "";
                //Console.WriteLine(ex.ToString());
            }
            
        }

        #endregion

        #region 图片的放大和缩小
        private void ChangImgSize(bool big)
        {
            Matrix m = specificOrganImage_img_SOIcanvas.RenderTransform.Value;
            System.Windows.Point p = new System.Windows.Point((specificOrganImage_img_SOIcanvas.ActualWidth) / 2, (specificOrganImage_img_SOIcanvas.ActualHeight) / 2);
            if (big)
            {
                m.ScaleAtPrepend(1.1, 1.1, p.X, p.Y);
            }
            else
            {
                m.ScaleAtPrepend(1 / 1.1, 1 / 1.1, p.X, p.Y);
            }
            specificOrganImage_img_SOIcanvas.RenderTransform = new MatrixTransform(m);
        }
        #endregion

        #region 放大命令
        private void GoBig()
        {
            try
            {
                if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                {
                    writeToTxt("放大");
                    ChangImgSize(true);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 缩小命令
        private void GoSmall()
        {
            try
            {
                if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                {
                    writeToTxt("缩小");
                    ChangImgSize(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 上移命令
        private void GoUp()
        {
            try
            {
                if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                {
                    writeToTxt("向上移动");
                    Matrix m = specificOrganImage_img_SOIcanvas.RenderTransform.Value;
                    //m.OffsetX = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetX + LayoutRoot.ActualWidth / 10;
                    m.OffsetY = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetY - LayoutRoot.ActualHeight * _beishu;
                    specificOrganImage_img_SOIcanvas.RenderTransform = new MatrixTransform(m);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 下移命令
        private void GoDown()
        {
            try
            {
                if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                {
                    writeToTxt("向下移动");
                    Matrix m = specificOrganImage_img_SOIcanvas.RenderTransform.Value;
                    //m.OffsetX = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetX + LayoutRoot.ActualWidth / 10;
                    m.OffsetY = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetY + LayoutRoot.ActualHeight * _beishu;
                    specificOrganImage_img_SOIcanvas.RenderTransform = new MatrixTransform(m);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 左移命令
        private void GoLeft()
        {
            try
            {
                if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                {
                    writeToTxt("向左移动");
                    Matrix m = specificOrganImage_img_SOIcanvas.RenderTransform.Value;
                    m.OffsetX = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetX - LayoutRoot.ActualWidth * _beishu;
                    //m.OffsetY = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetY + LayoutRoot.ActualHeight / 10;
                    specificOrganImage_img_SOIcanvas.RenderTransform = new MatrixTransform(m);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 右移命令
        private void GoRight()
        {
            try
            {
                if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                {
                    writeToTxt("向右移动");
                    Matrix m = specificOrganImage_img_SOIcanvas.RenderTransform.Value;
                    m.OffsetX = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetX + LayoutRoot.ActualWidth * _beishu;
                    //m.OffsetY = specificOrganImage_img_SOIcanvas.RenderTransform.Value.OffsetY + LayoutRoot.ActualHeight / 10;
                    specificOrganImage_img_SOIcanvas.RenderTransform = new MatrixTransform(m);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 返回命令
        private void GoBack()
        {
            try
            {
                writeToTxt("返回");
                if (patientInDepart_canvas.Visibility == Visibility.Visible)
                {
                    showCanvas(entry_canvas);
                }
                else if (specificOrganImageGroup_canvas.Visibility == Visibility.Visible)
                {
                    showCanvas(patientInDepart_canvas);
                }
                else if (specificOrganImage_canvas.Visibility == Visibility.Visible)
                {
                    showCanvas(specificOrganImageGroup_canvas);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 确定命令
        private void GoConfirm()
        {
            try
            {
                if (specificOrganImageGroup_canvas.Visibility == Visibility.Visible)
                {
                    writeToTxt("确定");
                    showCanvas(specificOrganImage_canvas);
                    specificOrganImage_img_SOIcanvas.Source = getBitmap(IMAGES[(int)_current]);
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region 上一张命令
        private void GoPre()
        {
            if (specificOrganImageGroup_canvas.Visibility == Visibility.Visible)
            {
                writeToTxt("上一张");
                if (_target > 0)
                    move(-1);
            }
        }
        #endregion

        #region 下一张命令
        private void GoNext()
        {
            if (specificOrganImageGroup_canvas.Visibility == Visibility.Visible)
            {
                writeToTxt("下一张");
                if (_target < _images.Count-1 )
                    move(1);
            }
        }
        #endregion

        #region 跳转到第N张图片
        private void GoToImage(int voiceImageIndex)
        {
            move(voiceImageIndex - (int)_current - 1);
            writeToTxt(voiceImageIndex.ToString());
            Console.WriteLine(DateTime.Now + ": 【语音操作】跳转到第" + voiceImageIndex + "张");
            //sentData(voiceImageIndex.ToString());
        }
        #endregion

        #region 中文数字转阿拉伯数字
        private string chineseNumberToNumber(string chineseNumberString)
        {
            string[,] arrayDictString = new string[20, 2];
            for (int i = 0; i < 20; ++i)
            {
                arrayDictString[i, 0] = (i + 1).ToString();
            }
            arrayDictString[0, 1] = "sdf";
            arrayDictString[1, 1] = "二";
            arrayDictString[2, 1] = "三";
            arrayDictString[3, 1] = "四";
            arrayDictString[4, 1] = "五";
            arrayDictString[5, 1] = "六";
            arrayDictString[6, 1] = "七";
            arrayDictString[7, 1] = "八";
            arrayDictString[8, 1] = "九";
            arrayDictString[9, 1] = "十";
            arrayDictString[10, 1] = "十一";
            arrayDictString[11, 1] = "十二";
            arrayDictString[12, 1] = "十三";
            arrayDictString[13, 1] = "十四";
            arrayDictString[14, 1] = "十五";
            arrayDictString[15, 1] = "十六";
            arrayDictString[16, 1] = "十七";
            arrayDictString[17, 1] = "十八";
            arrayDictString[18, 1] = "十九";
            arrayDictString[19, 1] = "二十";
            for (int i = 0; i < 20; ++i)
            {
                chineseNumberString = chineseNumberString.Replace(arrayDictString[i, 1], arrayDictString[i, 0]);
            }
            return chineseNumberString;
        }
        #endregion

        #region 中文数字转阿拉伯数字（数据库）
        private string chineseNumberToNumber_db(string chineseNumberString)
        {
            string _sql = "select * from chineseNumber";
            OleDbConnection objConnection = new OleDbConnection(strConnection);
            objConnection.Open();
            OleDbCommand sqlcmd = new OleDbCommand(@_sql, objConnection);
            OleDbDataReader reader = sqlcmd.ExecuteReader();
            while (reader.Read())
            {
                if (chineseNumberString.Contains(reader["中文数字"].ToString()))
                {
                    chineseNumberString = chineseNumberString.Replace(reader["中文数字"].ToString(), reader["阿拉伯数字"].ToString());
                }
            }
            objConnection.Close();
            reader.Close();
            return chineseNumberString;
        }
        #endregion

        #region 将命令写入.txt文件
        private void writeToTxt(string cmd)
        {
            //如果文件不存在，则创建；存在则覆盖
            //System.IO.File.WriteAllText(@"D:\cmd.txt", cmd, Encoding.UTF8);
        }
        #endregion

        #region
        private void sentData(string cmd)
        {
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);  
            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(serverIP), 3000);  
            client.Connect(ipe);  
            byte[] buffer = Encoding.Unicode.GetBytes(cmd);   
            client.Send(buffer);  
            //MessageBox.Show("发送成功！");  
        }
        #endregion
    }
}
