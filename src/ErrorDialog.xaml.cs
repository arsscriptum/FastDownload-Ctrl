using FastDownloader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

namespace FastDownloader
{
    public enum ErrorDialogMode
    {
        Error = 0,
        Cancelled = 1,
        Network = 2,
        MAX = 3
    }
    
    public partial class ErrorDialog : Window
    {
        public int LastErrorId { get; set; }
        public string Message { get; set; }
        public ErrorDialog(string message = "", ErrorDialogMode mode = ErrorDialogMode.Error)
        {
            InitializeComponent();
            if (mode == ErrorDialogMode.Cancelled)
            {
                Message = message;

                this.Title = "Cancelled";
                textMainTitle.Text = "Operation Cancelled";
            }
            else if (mode == ErrorDialogMode.Network)
            {
                Message = $"Network Error Occured: {LastErrorId}";
                this.Title = "Network Error";
                textMainTitle.Text = "Network Error";
            }
            else
            {
                if (message != "")
                {
                    Message = message;
                }
               
            }

            
            DataContext = this;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}