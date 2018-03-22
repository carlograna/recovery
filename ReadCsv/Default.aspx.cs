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

            // (savePath);

            ProcessXml(@"C:\Users\carlo\source\repos\read_csv\ReadCsv\uploads\TEST.ATT_PMT_V6_0_2018_02_21.xml");
        }

        protected void ProcessXml(string path)
        {
            // Loading from a file, you can also load from a stream
            var xml = XDocument.Load(path);

            #region HEADER
            var header = (
                from h in xml.Root.Descendants("Header")
                select new
                {
                    VersionNumber = h.Element("VersionNumber").Value
                    ,BillerGroupID = h.Element("BillerGroupID").Value
                    ,BillerGroupShortName = h.Element("BillerGroupShortName").Value
                    ,BillerID = h.Element("BillerID").Value
                    ,BillerShortName = h.Element("BillerShortName").Value
                    ,FileIndicator = h.Element("FileIndicator").Value
                    ,ProcessDate = h.Element("ProcessDate").Value
                    ,BillerReportName = h.Element("BillerReportName").Value
                }
                ).SingleOrDefault();

            lblHeader.Text = String.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}"
                                            , header.VersionNumber
                                            , header.BillerGroupID
                                            , header.BillerGroupShortName
                                            , header.BillerID
                                            , header.BillerShortName
                                            , header.FileIndicator
                                            , header.ProcessDate
                                            , header.BillerReportName);
            #endregion
            // Query the data and write out a subset of contacts
            #region TRANSACTIONS
            var transactions = from c in xml.Root.Descendants("TransactionDetail")
                            //where (int)c.Attribute("id") < 4
                                select new
                                {
                                    UserID = c.Element("UserId").Value,
                                    AmountDue = c.Element("AmountDue").Value,
                                    PaymentAmount = c.Element("PaymentAmount").Value,
                                    DueDate = c.Element("DueDate").Value,
                                    Parameter = c.Descendants("Parameter")
                                };

            foreach (var tran in transactions)
            {
                foreach (var par in tran.Parameter)
                {
                    lblTrans.Text += "<br/>" + par.Element("Name").Value + ", " + par.Element("Value").Value;
                }

                lblTrans.Text += String.Format("<br/>Transaction: {0}, {1}, {2}, {3}", tran.UserID, tran.AmountDue, tran.PaymentAmount, tran.DueDate);
            }
            #endregion

            #region SUMMARY
            var summary = from t in xml.Root.Descendants("PaymentStatusSummary")
                          //where (string)t.Attribute("type") == "SENT"
                          select new
                          {
                              TotalAmount = t.Element("TotalAmount").Value
                          };

            double totalAmt = 0;
            foreach(var sum in summary)
            {
                lblTrans.Text += "<br/> total: " + sum.TotalAmount;
                totalAmt += double.Parse(sum.TotalAmount);
            }
            #endregion

            int globalBatchID;

            using(var dbContext = new ItemDBContext())
            {
                // Key is equal to the current max value plus one
                globalBatchID = dbContext.Batch.Max(b => b.GlobalBatchID) + 1;
            }

            DateTime curDate = DateTime.Now;
            Batch batch = new Batch()
            {
                GlobalBatchID = globalBatchID
                , BankID = 7
                , LockboxID = 306
                , DESetupID = null
                , ProcessingDate = curDate.Date
                , BatchID = 203
                , DepositStatus = 850
                , BatchCueID = 0 // ???
                , CutOffStatus = 0
                , BatchMode = null
                , SystemType = 0
                , Priority = 1
                , PrintLabels = 1
                , BatchTypeCode = 50 // ???
                , BatchTypeSubCode = 0
                , BatchTypeText = null
                , ConsolidationStatus = 0
                , CARStatus = null
                , KFIStatus = null
                , FAXStatus = null
                , FloatAssignmentStatus = 0
                , ScanStatus = 21
                , DEStatus = 0
                , ExtractSequenceNumber = 0
                , ImageExtractSequenceNumber = null
                , DepositDate = curDate.Date
                , CheckCount = transactions.Count()
                , CheckAmount = (decimal) totalAmt
                , StubCount = transactions.Count()
                , StubAmount = (decimal) totalAmt
                , CreationDate = curDate
                , BillingTC = 0
                , BillingRT = 0
                , BillingAccount = 0
                , BillingSerial = 0
                , WorkgroupID = 1
                , CurrencyCode = null
                , MICRVerifyStatus = 4
                , DEPrinted = null
                , Is2Pass = 1
                , UseCar = 1
                , NoChecksEnclosed = 0
                , Rejects = 0
                , DepositTicketPrinted = false
                , DateLastUpdated = curDate
                , TransportID = null
                , TrayID = null
                , SubTrayID = null
                , UseCustomer = 0
                , DepositDDA = null
                , EncodeComplete = null
                , SplitsEnded = false
                , SiteCode = null
                , BitonalImageRemovalDate = null // ist seems this removal dates are a 1 1/2 month later
                , DataRemovalDate = null
                , ModificationDate = curDate
                , TransportBatchID = null
                , GrayScaleImageRemovalDate = null
                , ColorImageRemovalDate = null
                , CWDBDataRemovalDate = null
                , CWDBBitonalImageRemovalDate = null
                , CWDBGrayScaleImageRemovalDate = null
                , CWDBColorImageRemovalDate = null
                , BatchName = null
                , BatchTypeID = Guid.NewGuid()
                ,DecisionStatus = 0
                ,AllowDecisioning = false
                ,RequireDEBeforeTwoPass = false
                ,OriginalGlobalBatchID = null
                ,BatchExtractID = null
                ,IsImageExchange = false
                ,CaptureSiteCodeID = 10
                ,BatchSourceKey = 1
                ,NumOLDDaysRolled = 0
            };

            BatchDataEntry bde = new BatchDataEntry() {
                GlobalBatchID = globalBatchID
                ,ChecksStorageBoxNumber = null
                ,DocumentsStorageBoxNumber = null
                ,IronMountainBoxNumber = null
            };

            IEnumerable<Checks> checks = GetChecks(transactions, batch);
            IEnumerable<ChecksDataEntry> checksDE = GetChecksDataEntry(checks, batch);
            IEnumerable<Transactions> trans = GetTransactions(checks, batch);

            using (ItemDBContext dbContext = new ItemDBContext())
            {
                dbContext.Batch.Add(batch);
                dbContext.BatchDataEntry.Add(bde);

                foreach (var tran in trans)
                {
                    dbContext.Transactions.Add(tran);
                }

                foreach (var check in checks)
                {
                    dbContext.Checks.Add(check);
                }

                foreach(var cde in checksDE)
                {
                    dbContext.ChecksDataEntry.Add(cde);
                }

                //lblTrans.Text = dbContext.Database.Log.ToString();
                dbContext.SaveChanges();
                lblTrans.Text = batch.ToString();
            }
        }

        private IEnumerable<Transactions> GetTransactions(IEnumerable<Checks> checks, Batch batch)
        {
            foreach (var check in checks)
            {
                Transactions tran = new Transactions()
                {
                    GlobalBatchID = batch.GlobalBatchID,
                    TransactionID = check.TransactionID,
                };
                yield return tran;
             }
        }

        private IEnumerable<ChecksDataEntry> GetChecksDataEntry(IEnumerable<Checks> checks, Batch batch)
        {
            foreach( var check in checks)
            {
                ChecksDataEntry cde = new ChecksDataEntry()
                {
                    GlobalBatchID = batch.GlobalBatchID,
                    GlobalCheckID = check.GlobalCheckID,
                    TransactionID = check.TransactionID
                };

                yield return cde;
            }
        }

        private IEnumerable<Checks> GetChecks(IEnumerable<dynamic> transactions, Batch batch)
        {
            int globalCheckID = GetCheckID();

            int i = 0;
            foreach (var tran in transactions)
            {
                i++; //start at 1
                Checks check = new Checks()
                {
                    GlobalCheckID = globalCheckID + i,
                    GlobalBatchID = batch.GlobalBatchID,
                    TransactionID = i,
                    TransactionSequence = i,
                    BatchSequence = i,
                    CheckSequence = i,
                    Status = null,
                    RawMICR = null,
                    RT = null,
                    Serial = null,
                    Account = null,
                    Amount = decimal.Parse(tran.PaymentAmount),
                    KilledMethod = 6
                };

                yield return check;
            }
        }

        private static int GetCheckID()
        {
            try
            {
                int globalCheckID;

                using (var dbContext = new ItemDBContext())
                {
                    globalCheckID = dbContext.Checks.Max(c => c.GlobalCheckID);
                }

                return globalCheckID;
            }
            catch
            {
                throw;
            }
        }

        protected void ProcessCsv(string path)
        {

            using (StreamReader sr = new StreamReader(path))
            {
                string current_line;
                while ((current_line = sr.ReadLine()) != null)
                {
                    lblTrans.Text += "<br/>" + current_line;
                }
            }

        }
    }
}