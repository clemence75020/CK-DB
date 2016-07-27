﻿using CK.Setup;
using CK.SqlServer.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DB.Res
{
    [SqlPackage( Schema = "CK", ResourcePath = "Res" )]
    public class Package : SqlPackage
    {
        /// <summary>
        /// Gets the tRes table from this package.
        /// </summary>
        [InjectContract]
        public ResTable ResTable { get; protected set; }
    }
}
