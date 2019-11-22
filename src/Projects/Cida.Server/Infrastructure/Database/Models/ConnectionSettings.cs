﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Cida.Server.Infrastructure.Database.Models
{
    public class ConnectionSettings
    {
        public string ServerName { get; set; }

        public string DatabaseName { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }
    }
}
