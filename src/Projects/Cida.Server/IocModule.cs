﻿using System;
using System.Collections.Generic;
using System.Text;
using Autofac;

namespace Cida.Server
{
    public class IocModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
        }
    }
}
