﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace Telegram
{
    public partial class SignupCode_Control : UserControl
    {
        public SignupCode_Control()
        {
            InitializeComponent();
        }

        public void SetCodeInvalid() {
            CodeInvalidTextBlock.Visibility = Visibility.Visible;
        }

        public string GetCode() {
            return Code.Text;
        }

        public void SetTimerTime(int seconds) {
            TimerTextBlock.Text = "We will call you in 0:" + seconds.ToString("00");
        }

        public void CallingNow() {
            TimerTextBlock.Text = "We are calling you now";
        }
    }
}
