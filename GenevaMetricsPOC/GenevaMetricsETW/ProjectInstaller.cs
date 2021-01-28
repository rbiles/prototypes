// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;

namespace LogAnalyticsOdsApiHarness
{
    using System;
    using System.ComponentModel;
    using System.Configuration.Install;

    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void serviceInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
        }

        private void serviceProcessInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {
        }
    }
}