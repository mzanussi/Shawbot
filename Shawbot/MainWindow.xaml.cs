using log4net;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Shawbot
{
    /// <summary>
    /// Sends the contents of a string array to Twitter at set intervals.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string KEYS = @"C:\Temp\Data\keys";             // Twitter keys
        private const string FILELIST = @"C:\Temp\Data\filelist";     // the list of files to process
        private const int TWEET_LEN = 140;
        private const string CONTINUED = "...";

        private string timestamp = "";                      // process start time

        private int fileNo = 0;                             // file number being processed (0-based)
        private int lineNo = 0;                             // line number to process (0-based)
        private string filename;                            // current file being processed
        private string[] fileContents;                      // contents of the file
        private string kHashtag;                            // text hashtag for Tweeting

        // Twitter keys.
        private string accessToken;
        private string accessTokenSecret;
        private string consumerKey;
        private string consumerSecret;

        // A timer and its interval.
        private DispatcherTimer timer = new DispatcherTimer();
        private const int INTERVAL = 2;    // in minutes

        // log4net
        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MainWindow()
        {
            InitializeComponent();

            log.Info("*** Shawbot launched ***");

            // The key file consists of the following information, each on its own line:
            //   access token
            //   access token secret
            //   consumer key
            //   consumer secret
            try
            {
                string[] keys = File.ReadAllLines(KEYS);
                accessToken = keys[0];
                accessTokenSecret = keys[1];
                consumerKey = keys[2];
                consumerSecret = keys[3];
                log.Info("Twitter keys loaded successfully!");
            }
            catch (FileNotFoundException)
            {
                log.Error("FileNotFoundException: Cannot load Twitter key file.");
                lblStatus.Content = "Cannot load Twitter key file.";
                lblStatus.Foreground = new SolidColorBrush(Colors.Red);
                btnStart.IsEnabled = false;
                btnReset.IsEnabled = false;
                return;
            }
            catch (Exception)
            {
                log.Error("Exception: Problem encountered loading Twitter keys.");
                lblStatus.Content = "Cannot load Twitter keys, problem with file.";
                lblStatus.Foreground = new SolidColorBrush(Colors.Red);
                btnStart.IsEnabled = false;
                btnReset.IsEnabled = false;
                return;
            }

            // open text file for processing
            if (!OpenFileForProcessing())
            {
                return;
            }

            // timer setup
            timer.Tick += new EventHandler(dispatchTimer_Tick);
            timer.Interval = new TimeSpan(0, 0, INTERVAL);

            // update UI
            btnStart.Focus();
        }

        private void dispatchTimer_Tick(object sender, EventArgs e)
        {
            // fetch next line to tweet
            string tweet = fileContents[lineNo];
            // update UI
            lblCurrentFile.Content = filename;
            lblLine.Content = "" + lineNo;
            tbTweet.Text = tweet;
            lblLength.Content = tweet.Length;
            log.Info("TWEET (" + tweet.Length + "): " + tweet);
            // tweet it
            // TEST ONLY
            Debug.Assert(tweet.Length <= 140, "found tweet > 140 chars");
            // update tweet status
            lblStatus.Content = "SUCCESS!  (" + DateTime.Now + ")";
            lblStatus.Foreground = new SolidColorBrush(Colors.Green);
            // if successful: 
            //   increment last line processed and update UI
            lineNo++;
            //   if eof, get next file name to process, update internal fileno, reset last line to 0
            //   update internal lastline
            if (lineNo >= fileContents.Length)
            {
                log.Info("Completed processing " + filename + ". Open next file.");
                fileNo++;
                Shawbot.Properties.Settings.Default.Fileno = fileNo;
                Shawbot.Properties.Settings.Default.Lineno = 0;
                Shawbot.Properties.Settings.Default.Save();
                // todo: check return
                OpenFileForProcessing();
            }
            else
            {
                Shawbot.Properties.Settings.Default.Lineno = lineNo;
                Shawbot.Properties.Settings.Default.Save();
            }
        }

        /// <summary>
        /// Open a file for processing. First get the file number and current line to
        /// process from settings, then open the file and load into a string array.  
        /// The files aren't altogether too large so this is acceptable.
        /// </summary>
        private bool OpenFileForProcessing()
        {
            log.Info("Open file for processing.");

            // From app settings, get file number and current line to process.
            fileNo = Shawbot.Properties.Settings.Default.Fileno;
            lineNo = Shawbot.Properties.Settings.Default.Lineno;

            try
            {
                // Retrieve the file list.
                string[] filelist = File.ReadAllLines(FILELIST);
                log.Info("File list length: " + filelist.Length);

                if (filelist.Length > 0)
                {
                    // if past the last file in the list, reset to 0.
                    if (fileNo >= filelist.Length)
                    {
                        Shawbot.Properties.Settings.Default.Fileno = 0;
                        Shawbot.Properties.Settings.Default.Lineno = 0;
                        Shawbot.Properties.Settings.Default.Save();
                    }
                    // Load the file into memory and proceed from there. The files
                    // aren't generally very long so this should not be an issue.
                    filename = filelist[fileNo];
                    fileContents = Tweetify(filename);
                    // Log useful info
                    log.Info("File number: " + fileNo);
                    log.Info("Line number: " + lineNo);
                    log.Info("Filename: " + filename);
                    if (fileContents == null)
                    {
                        return false;
                    }
                    else
                    {
                        log.Info("File contents length: " + fileContents.Length + " lines");
                    }
                }
                else
                {
                    log.Error("Filelist appears empty.");
                    lblCurrentFile.Content = "No files in filelist to process.";
                    lblCurrentFile.Foreground = new SolidColorBrush(Colors.Red);
                    btnStart.IsEnabled = false;
                    btnReset.IsEnabled = false;
                    return false;
                }
            }
            catch (FileNotFoundException)
            {
                log.Error("FileNotFoundException: Cannot load filelist.");
                lblCurrentFile.Content = "Cannot load filelist.";
                lblCurrentFile.Foreground = new SolidColorBrush(Colors.Red);
                btnStart.IsEnabled = false;
                btnReset.IsEnabled = false;
                return false;
            }
            catch (Exception)
            {
                log.Error("Exception: Problem encountered loading filelist.");
                lblCurrentFile.Content = "Cannot load filelist, problem with file.";
                lblCurrentFile.Foreground = new SolidColorBrush(Colors.Red);
                btnStart.IsEnabled = false;
                btnReset.IsEnabled = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Given a line of characters, returns an ArrayList breaking down the
        /// line into a series of lines less than are equal to 140 characters.
        /// </summary>
        private void ChunkIt(string line, ArrayList lines)
        {
            if (line.Length + 1 + kHashtag.Length <= TWEET_LEN)
            {
                lines.Add(line + " " + kHashtag);
            }
            else
            {
                int ptr = TWEET_LEN - CONTINUED.Length - 1 - kHashtag.Length - 1;
                while (line[ptr] != ' ')
                {
                    ptr--;
                }
                string str = line.Substring(0, ptr);
                str = str.TrimEnd() + CONTINUED + " " + kHashtag;
                lines.Add(str);
                ChunkIt(line.Substring(ptr + 1), lines);
            }
        }

        /// <summary>
        /// Give it a Gutenberg text file and get back a 
        /// string array of Tweetable strings.
        /// </summary>
        public string[] Tweetify(string filename)
        {
            ArrayList contents = new ArrayList();

            bool isDone = false;
            int line_count = 0;

            using (StreamReader sr = new StreamReader(filename))
            {
                // First line of file is a hashtag name for file.
                // Stop if no hashtag was found.
                if (sr.Peek() >= 0)
                {
                    string line = sr.ReadLine();
                    if (line[0] != '#')
                    {
                        log.Error("Hashtag not found in file " + filename);
                        lblStatus.Content = "Hashtag not found in file " + filename;
                        lblStatus.Foreground = new SolidColorBrush(Colors.Red);
                        btnStart.IsEnabled = false;
                        btnReset.IsEnabled = false;
                        return null;
                    } else
                    {
                        kHashtag = line;
                    }
                }
                else
                {
                    log.Error("Unexpected end of file encountered.");
                    lblStatus.Content = "Unexpected end of file encountered.";
                    lblStatus.Foreground = new SolidColorBrush(Colors.Red);
                    btnStart.IsEnabled = false;
                    btnReset.IsEnabled = false;
                    return null;
                }

                while (!isDone)
                {
                    string cur_line = "";

                    // Read in text until end of paragraph has been reached
                    // (empty line) or end of file is encountered. Append
                    // the text to the current line.
                    while (true)
                    {
                        if (sr.Peek() >= 0)
                        {
                            string line = sr.ReadLine();

                            if (string.IsNullOrEmpty(line))
                            {
                                break;
                            }
                            cur_line += line + " ";
                        }
                        else
                        {
                            // end of file
                            isDone = true;
                            break;
                        }
                    }

                    if (cur_line.Length > 0)
                    {
                        // Trim line first.
                        cur_line = cur_line.TrimEnd();
                        // If line is <= 140 characters (with hashtag added) just output 
                        // the line plus the hashtag. Otherwise, break up line into 
                        // <= 140 chararacter chunks.
                        if (cur_line.Length + 1 + kHashtag.Length <= TWEET_LEN)
                        {
                            line_count++;
                            contents.Add(cur_line + " " + kHashtag);
                        }
                        else
                        {
                            ArrayList lines = new ArrayList();
                            ChunkIt(cur_line, lines);
                            foreach (string line in lines)
                            {
                                line_count++;
                                contents.Add(line);
                            }
                        }
                    }
                }
            }

            return (string[])contents.ToArray(typeof(string));
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            log.Info("Start button clicked.");
            // UI setup
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            btnStop.Focus();
            btnReset.IsEnabled = false;

            timestamp = DateTime.Now.ToString();
            lblTimestamp.Content = "(running since " + timestamp + ")";

            // start the timer
            timer.Start();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            log.Info("Stop button clicked.");

            btnStart.IsEnabled = true;
            btnStart.Focus();
            btnStop.IsEnabled = false;
            btnReset.IsEnabled = true;
            lblTimestamp.Content = "";
            // stop the timer
            timer.Stop();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            log.Info("Reset button clicked.");

            // reset parameters (current file # to 0, current line # to 0)
            Shawbot.Properties.Settings.Default.Fileno = 0;
            Shawbot.Properties.Settings.Default.Lineno = 0;
            Shawbot.Properties.Settings.Default.Save();
            lblCurrentFile.Content = "";
            lblLine.Content = "";
            tbTweet.Text = "";
            lblLength.Content = "";
            lblStatus.Content = "";

            // now open file for processing
            if (!OpenFileForProcessing())
            {
                return;
            }
        }
    }
}
