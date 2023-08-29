using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Diagnostics;
using System;
using System.IO;
using System.Threading.Channels;

namespace BetaCensor.Core
{

    public partial class TypeForMeWindow : Window
        {

        //Bits needed for continuous window focus.
        delegate void DelegateGetFocus();
        private DelegateGetFocus m_getFocus;
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(HandleRef hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr WindowHandle);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
        private const int SW_RESTORE = 9;
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);
        private const short SWP_NOMOVE = 0X2;
        private const short SWP_NOSIZE = 1;
        private const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        //Form Specfic bits
        TextBox txtBox;
        TextBlock instructionsLabel;
        TextBlock statsLabel;
        private Grid grid;
        int linesLeft = 1;
        int mistakes = 0;
        int mistakePenalty = 0;
        int timePenalty = 0;
        int timeSinceKeystroke = 0;
        int totalTime = 0;
        bool finished = false;
        bool started = false;
        string lineToWrite = "hello";
        Func<int, int, Task> finishedFunc;

        public TypeForMeWindow(int linesToWrite, string line, int typoPenalty, int timeLimitPenalty, int timeElasped, int mistakesTotal, Func<int, int, Task> onFinish)
        {
            //Setup the window
            Title = "Type for me";
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            linesLeft = linesToWrite;
            lineToWrite = line;
            mistakePenalty = typoPenalty;
            mistakes = mistakesTotal;
            timePenalty = timeLimitPenalty;
            totalTime = timeElasped;
            finishedFunc = onFinish;
            writeStateToFile();
            //setup form for users input
            txtBox = new TextBox();
            txtBox.Width = SystemParameters.PrimaryScreenWidth / 2;
            txtBox.Height = SystemParameters.PrimaryScreenHeight / 20;
            //Setup events that listens on keypress
            txtBox.KeyUp += txtBoxKeyPress;
            txtBox.KeyDown += txtBoxKeyPressNop;
            txtBox.Visibility = Visibility.Visible;


            instructionsLabel = new TextBlock();
            instructionsLabel.Text = $"Write the following:\n{lineToWrite}\n";
            instructionsLabel.Width = SystemParameters.PrimaryScreenWidth / 2;
            instructionsLabel.TextWrapping = TextWrapping.Wrap;
            instructionsLabel.VerticalAlignment = VerticalAlignment.Bottom;
            instructionsLabel.Visibility = Visibility.Visible;

            statsLabel = new TextBlock();
            statsLabel.Text = $"Lines left: {linesLeft}\nMistakes: {mistakes}\nTime since last keystroke: {timeSinceKeystroke} seconds\nTotal time: {totalTime} seconds\n";
            statsLabel.Width = SystemParameters.PrimaryScreenWidth / 2;
            statsLabel.Visibility = Visibility.Visible;

            grid = new Grid();
            ColumnDefinition col = new ColumnDefinition();
            grid.ColumnDefinitions.Add(col);

            RowDefinition instructionsRow = new RowDefinition();
            RowDefinition typingRow = new RowDefinition();
            typingRow.Height = GridLength.Auto;
            RowDefinition statsRow = new RowDefinition();

            grid.RowDefinitions.Add(instructionsRow);
            grid.RowDefinitions.Add(typingRow);
            grid.RowDefinitions.Add(statsRow);

            grid.Children.Add(instructionsLabel);
            Grid.SetColumn(instructionsLabel, 0);
            Grid.SetRow(instructionsLabel, 0);

            grid.Children.Add(txtBox);
            Grid.SetColumn(txtBox, 0);
            Grid.SetRow(txtBox, 1);

            grid.Children.Add(statsLabel);
            Grid.SetColumn(statsLabel, 0);
            Grid.SetRow(statsLabel, 2);

            grid.Visibility = Visibility.Visible;
            AddChild(grid);

            // disable controls and is obnoxious about giving them up
            Closing += WindowClosing;
            ResizeMode = ResizeMode.NoResize;
            m_getFocus = new DelegateGetFocus(performBackgroundUpdates);
            spawnThread(updateBackground);
        }

        void txtBoxKeyPressNop(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl)
            {
                linesLeft+= mistakePenalty;
                txtBox.Text = "Cheater";
                writeStateToFile();
                e.Handled = true;
                statsLabel.Text = $"Lines left: {linesLeft}\nMistakes: {mistakes}\nTime since last keystroke: {timeSinceKeystroke} seconds\nTotal time: {totalTime} seconds\n";
                return;
            }
        }
   
        void txtBoxKeyPress(object sender, KeyEventArgs e)
        {
            started = true;
            timeSinceKeystroke = 0;
            if (e.Key == Key.LeftCtrl)
            {
                linesLeft += mistakePenalty;
                writeStateToFile();
                txtBox.Text = "Cheater";
                e.Handled = true;
                statsLabel.Text = $"Lines left: {linesLeft}\nMistakes: {mistakes}\nTime since last keystroke: {timeSinceKeystroke} seconds\nTotal time: {totalTime} seconds\n";
                return;
            }

            string currentText = txtBox.Text;
            if (!lineToWrite.StartsWith(currentText))
            {
                linesLeft += mistakePenalty;
                txtBox.Text = "";
                writeStateToFile();
            }
            if (currentText == lineToWrite)
            {
                linesLeft--;
                txtBox.Text = "";
                writeStateToFile();
                if (linesLeft <= 0){
                    deleteStateFile();
                    finishedFunc(totalTime, mistakes);
                    finished = true;
                    Close();
                }
            }
        }
        void WindowClosing(object sender, CancelEventArgs e)
        {
            if (!finished)
            {
                e.Cancel = true;
            }

        }

        //Spawns a new Thread.
        private void spawnThread(ThreadStart ts)
        {
            try
            {
                Thread newThread = new Thread(ts);
                newThread.IsBackground = true;
                newThread.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to spawn background thread: {e}");
            }
        }

        //Continuously call update background.
        private void updateBackground()
        {
            while (true)
            {
                performBackgroundUpdates();
                Thread.Sleep(1000);
            }
        }

        private void deleteStateFile()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string dirPath = "AllSorts_Censor";
            string filePath = "type-for-me";
            string fullFilePath = Path.Combine(folder, dirPath, filePath);
            Directory.CreateDirectory(Path.Combine(folder, "AllSorts_Censor"));
            File.Delete(fullFilePath);
        }
        private void writeStateToFile()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string dirPath = "AllSorts_Censor";
            string filePath = "type-for-me";
            string fullFilePath = Path.Combine(folder, dirPath, filePath);
            Directory.CreateDirectory(Path.Combine(folder, "AllSorts_Censor"));
            //Write text as the discord command again as it gets read and triggered by the discord bot
            string discordCommandText = $"!type-for-me times={linesLeft} penalty={mistakePenalty} time_penalty={timePenalty} text=\"{lineToWrite}\" time_elasped={totalTime} mistakes={mistakes}";
            File.WriteAllText(fullFilePath, discordCommandText);
        }

        //Keeps Form on top and gives focus.
        private void performBackgroundUpdates()
        {
            //If we need to invoke this call from another thread.
            if (!Dispatcher.CheckAccess())
            {
                try
                {
                  Dispatcher.Invoke(m_getFocus, new object[] { });
                }
                catch (System.ObjectDisposedException e)
                {
                    // do nothing with the exception, just swallow it.
                }
            }
            //Otherwise, we're safe.
            else
            {
                //timePenalty + updating UI
                if (started)
                {
                    if (timePenalty != 0 && timeSinceKeystroke > timePenalty)
                    {
                        timeSinceKeystroke = 0;
                        linesLeft += mistakePenalty;
                    }
                    totalTime++;
                }
                timeSinceKeystroke++;
                statsLabel.Text = $"Lines left: {linesLeft}\nMistakes: {mistakes}\nTime since last keystroke: {timeSinceKeystroke} seconds\nTotal time: {totalTime} seconds\n";
                
                //forcing window to top corner
                Top = 0;
                Left = 0;
                Topmost = true;
                Activate();
                Focus();

                //forcibly stealing focus
                string name = Process.GetCurrentProcess().ProcessName;
                Process[] objProcesses = Process.GetProcessesByName(name);
                var procId = Process.GetCurrentProcess().Id;
                int activeProcId;
                var activatedHandle = GetForegroundWindow();
                GetWindowThreadProcessId(activatedHandle, out activeProcId);
                if (activeProcId != procId)
                {
                    IntPtr hWnd = objProcesses[0].MainWindowHandle;
                    SetWindowPos(hWnd, (int)HWND_TOPMOST, 0, 0, 0, 0, (int)TOPMOST_FLAGS);
                    SetWindowPos(hWnd, (int)HWND_NOTOPMOST, 0, 0, 0, 0, (int)TOPMOST_FLAGS);
                    ShowWindow(new HandleRef(null, hWnd), SW_RESTORE);
                    SetForegroundWindow(hWnd);
                }
            }
        }
    }
}
