using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;

namespace ReverseRatClient
{
    class Funciones
    {
      


        public string FormarCadena(DataGridView x)
        {
            string cadena = "";
            int Indice = 0;
            if (x.SelectedRows.Count > 0)
            {
                Indice  = x.SelectedRows[0].Index;
                cadena = "M1X3R|" + x["IPEquipo", Indice].Value + "|" + x["NombrePC", Indice].Value + "|" + x["SistemaOperativo", Indice].Value + "|" + x["Mutex", Indice].Value;
            }
            return cadena;
        }
    }
}
