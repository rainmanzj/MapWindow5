﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MW5.Api.Interfaces;

namespace MW5.Plugins.Interfaces
{
    public interface IMainForm
    {
        object MenuManager { get;}
        IMapControl Map { get; }
    }
}