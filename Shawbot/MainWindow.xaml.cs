﻿using log4net;
using System;
using System.Collections;
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
        private int lastLine = 0;                           // last line processed
        private string filename;                            // current file being processed
        private string[] file;                              // contents of the file
        private string hashtag;

        // Twitter keys.
        private string accessToken;
        private string accessTokenSecret;
        private string consumerKey;
        private string consumerSecret;

        // A timer and its interval.
        private DispatcherTimer timer = new DispatcherTimer();
        private const int INTERVAL = 10;    // in minutes

        // log4net
        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MainWindow()
        {
            InitializeComponent();

            log.Info("Shawbot started.");

            // open text file for processing
            if (!OpenFileForProcessing())
            {
                return;
            }

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
                tbStatus.Text = "Cannot load Twitter key file.";
                tbStatus.Foreground = new SolidColorBrush(Colors.Red);
                btnStart.IsEnabled = false;
                btnReset.IsEnabled = false;
                return;
            }
            catch (Exception)
            {
                log.Error("Exception: Problem encountered loading Twitter keys.");
                tbStatus.Text = "Cannot load Twitter keys, problem with file.";
                tbStatus.Foreground = new SolidColorBrush(Colors.Red);
                btnStart.IsEnabled = false;
                btnReset.IsEnabled = false;
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
            //    TODO: bounds checking
            string tweet = file[lastLine + 1];
            // tweet it
            tbTweet.Text = tweet;
            lblLength.Content = tweet.Length;
            // update status line
            //   (test)
            tbStatus.Text = "SUCCESS!  (" + DateTime.Now + ")";
            // if successful: 
            //   increment last line processed and update UI
            lastLine++;
            lblLine.Content = "" + lastLine;
            //   if eof, get next file name to process, update internal fileno, reset last line to 0
            //   update internal lastline
            Shawbot.Properties.Settings.Default.Lastline = lastLine;
            Shawbot.Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Open a file for processing. First get the file number and last line processed
        /// from settings, then open the file and load into an array. The files aren't
        /// altogether too large so this is acceptable.
        /// </summary>
        private bool OpenFileForProcessing()
        {
            log.Info("Open file for processing...");

            // From app settings, get file number and last line processed.
            fileNo = Shawbot.Properties.Settings.Default.Fileno;
            lastLine = Shawbot.Properties.Settings.Default.Lastline;
            log.Info("File number: " + fileNo);
            log.Info("Last line: " + lastLine);

            try
            {
                // Retrieve the file list.
                string[] filelist = File.ReadAllLines(FILELIST);
                log.Info("File list length: " + filelist.Length);

                if (filelist.Length > 0)
                {
                    // open file for processing
                    filename = filelist[fileNo];
                    // or better, just load the file into memory and
                    // proceed from there. the files aren't generally
                    // very long so this should not be an issue.
                    file = Tweetify(filename);

                } else
                {
                    log.Error("Filelist appears empty.");
                    lblCurrentFile.Content = "No files in filelist to process.";
                    lblCurrentFile.Foreground = new SolidColorBrush(Colors.Red);
                    return false;
                }
            }
            catch (FileNotFoundException)
            {
                log.Error("FileNotFoundException: Cannot load filelist.");
                lblCurrentFile.Content = "Cannot load filelist.";
                lblCurrentFile.Foreground = new SolidColorBrush(Colors.Red);
                return false;
            }
            catch (Exception)
            {
                log.Error("Exception: Problem encountered loading filelist.");
                lblCurrentFile.Content = "Cannot load filelist, problem with file.";
                lblCurrentFile.Foreground = new SolidColorBrush(Colors.Red);
                return false;
            }

            // update UI
            lblCurrentFile.Content = filename;
            lblLine.Content = "";
            if (lastLine != 0)
            {
                lblLine.Content = "" + lastLine;
            }
            tbTweet.Text = "";
            tbStatus.Text = "";

            return true;
        }

        /// <summary>
        /// Given a line of characters, returns an ArrayList breaking down the
        /// line into a series of lines less than are equal to 140 characters.
        /// </summary>
        private void ChunkIt(string line, ArrayList lines)
        {
            if (line.Length + 1 + hashtag.Length <= TWEET_LEN)
            {
                lines.Add(line + " " + hashtag);
            }
            else
            {
                int ptr = TWEET_LEN - CONTINUED.Length - 1 - hashtag.Length - 1;
                while (line[ptr] != ' ')
                {
                    ptr--;
                }
                string str = line.Substring(0, ptr);
                str = str.TrimEnd() + CONTINUED + " " + hashtag;
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
                // TODO: first line is hashtag name for file
                if (sr.Peek() >= 0)
                {
                    string line = sr.ReadLine();
                    if (line[0] != '#')
                    {
                        log.Error("Hashtag not found.");
                        tbStatus.Text = "Hashtag not found.";
                        tbStatus.Foreground = new SolidColorBrush(Colors.Red);
                        return null;
                    } else
                    {
                        hashtag = line;
                    }
                }
                else
                {
                    log.Error("Unexpected end of file encountered.");
                    tbStatus.Text = "Unexpected end of file encountered.";
                    tbStatus.Foreground = new SolidColorBrush(Colors.Red);
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
                        if (cur_line.Length + 1 + hashtag.Length <= TWEET_LEN)
                        {
                            line_count++;
                            contents.Add(cur_line + " " + hashtag);
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
            // UI setup
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            btnStop.Focus();
            btnReset.IsEnabled = false;
            ckNoTweet.IsEnabled = false;

            timestamp = DateTime.Now.ToString();
            lblTimestamp.Content = "(running since " + timestamp + ")";

            // start the timer
            timer.Start();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = true;
            btnStart.Focus();
            btnStop.IsEnabled = false;
            btnReset.IsEnabled = true;
            ckNoTweet.IsEnabled = true;
            lblTimestamp.Content = "";
            // stop the timer
            timer.Stop();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            // reset parameters (current file # to 1, current line # to 1)
            Shawbot.Properties.Settings.Default.Fileno = 0;
            Shawbot.Properties.Settings.Default.Lastline = 0;
            Shawbot.Properties.Settings.Default.Save();
            // now open file for processing
            if (!OpenFileForProcessing())
            {
                return;
            }
        }
    }
}
