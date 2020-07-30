using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;


namespace ThermostatSimuate
{
    class GlobalVar
    {
        //public static DataTable dt { get; set; }
        public static SqlDataAdapter da { get; set; }
        public static SqlCommandBuilder bu { get; set; }

    }
}
