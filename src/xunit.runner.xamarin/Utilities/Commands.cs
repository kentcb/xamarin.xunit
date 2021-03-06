﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace Xunit.Runners.Utilities
{
    internal static class Commands
    {
        static Commands()
        {
            LaunchUrl = new Command<string>(OnLaunchUrl);
        }

        public static ICommand LaunchUrl { get; private set; }

        private static void OnLaunchUrl(string str)
        {
            Device.OpenUri(new Uri(str));
        }
    }
}