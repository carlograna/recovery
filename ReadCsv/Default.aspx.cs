using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using System.Xml.Linq;

namespace ReadCsv
{
    public partial class _Default : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            //string savePath = @"C:\Users\carlo\source\repos\read_csv\ReadCsv\uploads\";

            //if (fileUpload.HasFile)
            //{
            //    savePath += fileUpload.FileName;
            //    fileUpload.SaveAs(savePath);
            //}

            //ProcessXml(savePath);

            ProcessXml(@"C:\Users\carlo\source\repos\read_csv\ReadCsv\uploads\TEST.ATT_PMT_V6_0_2018_02_21.xml");
        }

        protected void ProcessXml(string path)
        {
            // Loading from a file, you can also load from a stream
            var xml = XDocument.Load(path);


            // Query the data and write out a subset of contacts
            var query = from c in xml.Root.Descendants("TransactionDetail")
                            //where (int)c.Attribute("id") < 4
                        select new
                        {
                            UserID = c.Element("UserId").Value
                               ,
                            AmountDue = c.Element("AmountDue").Value
                                ,
                            PaymentAmount = c.Element("PaymentAmount").Value
                                ,
                            DueDate = c.Element("DueDate").Value
                            //,
                            //Parameters = c.Element("Paramters").Value
                            ,
                            Parameter = c.Descendants("Parameter")

                        };

            foreach (var trans in query)
            {
                foreach(var par in trans.Parameter)
                {
                    lblMessage.Text += "<br/>" + par.Element("Name").Value + ", " + par.Element("Value").Value;
                }

                lblMessage.Text += String.Format("<br/>Transaction: {0}, {1}, {2}, {3}", trans.UserID, trans.AmountDue, trans.PaymentAmount, trans.DueDate);
            }
        }
        protected void ProcessCsv(string path)
        {

            using (StreamReader sr = new StreamReader(path))
            {
                string current_line;
                while ((current_line = sr.ReadLine()) != null)
                {
                    lblMessage.Text += "<br/>" + current_line;
                }
            }

        }
    }
}