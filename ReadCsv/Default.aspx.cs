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
    public class BankTran
    {
        public string UserID { get; set; }
        public string AmountDue { get; set; }
        public string PaymentAmount { get; set; }
        public string DueDate { get; set; }
        public string ArpID { get; set; }
        public string RecoveryPaymentType { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
    public partial class _Default : Page
    {
        protected delegate bool PaymentHandler(string pmtType);

        //static bool IsMisappliedPmt(string pmtType) { return pmtType == "OVP"; }
        //static bool IsReturnedPmt(string pmtType) { return pmtType == "BRI_ACH" || pmtType == "BRI_CHK"; }

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
                    VersionNumber = h.Element("VersionNumber").Value,
                    BillerGroupID = h.Element("BillerGroupID").Value,
                    BillerGroupShortName = h.Element("BillerGroupShortName").Value,
                    BillerID = h.Element("BillerID").Value,
                    BillerShortName = h.Element("BillerShortName").Value,
                    FileIndicator = h.Element("FileIndicator").Value,
                    ProcessDate = h.Element("ProcessDate").Value,
                    BillerReportName = h.Element("BillerReportName").Value
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
                                   Parameter = c.Descendants("Parameter"),
                                   FirstName = c.Descendants("FirstName"),
                                   LastName = c.Descendants("LastName")
                               };

            IEnumerable<BankTran> bankTrans = BankTrans(transactions);

            //IEnumerable<BankTran> listOVP = FilterTransactions(new PaymentHandler(IsMisappliedPmt), BankTrans);
            //IEnumerable<BankTran> listBRI = FilterTransactions(new PaymentHandler(IsReturnedPmt), BankTrans);
            IEnumerable<BankTran> overpaidTrans = FilterTransactions(s => s == "OVP", bankTrans);
            IEnumerable<BankTran> briTrans = FilterTransactions(s => s == "BRI_CHK" || s == "BRI_ACH", bankTrans);

            List<IEnumerable<BankTran>> list = new List<IEnumerable<BankTran>>();

            list.Add(overpaidTrans);
            list.Add(briTrans);

            CreateTransOnDB(list);
            #endregion

            #region  CheckTotal

            // Summary totals
            var summary = from t in xml.Root.Descendants("PaymentStatusSummary")
                              //where (string)t.Attribute("type") == "SENT"
                          select new
                          {
                              TotalAmount = t.Element("TotalAmount").Value
                          };

            double totalSummaryAmt = 0;
            foreach (var sum in summary)
            {
                lblTrans.Text += "<br/> total: " + sum.TotalAmount;
                totalSummaryAmt += double.Parse(sum.TotalAmount);
            }

            // Transaction totals
            double totalTranAmt = 0;
            foreach (var subList in list)
            {
                foreach (var tran in subList)
                {
                    lblTrans.Text += String.Format("<br/>{0} Transaction: {1}, {2}, {3}, {4}, {5}", tran.RecoveryPaymentType, tran.UserID, tran.AmountDue, tran.PaymentAmount, tran.DueDate, tran.ArpID);
                    totalTranAmt += Convert.ToDouble(tran.PaymentAmount);
                }
            }

            // Checking totals
            if (totalTranAmt == totalSummaryAmt)
                lblMsg.Text = "Total transaction and summary amount match.";
            else
                lblMsg.Text = "Total transaction and summary amounts DO NOT match.";
            #endregion            
        }

        private void CreateTransOnDB(List<IEnumerable<BankTran>> List)
        {
            try
            {
                using (ItemDBContext dbContext = new ItemDBContext())
                {
                    foreach (var subList in List)
                    {
                        Batch batch = CreateBatch(subList);
                        BatchDataEntry bde = CreateBatchDataEntry(batch.GlobalBatchID);

                        IEnumerable<Checks> checks = CreateChecks(subList, batch);
                        IEnumerable<ChecksDataEntry> checksDE = CreateChecksDataEntry(checks, batch, subList.ToList());
                        IEnumerable<Transactions> trans = CreateTransactions(checks, batch);

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

                        foreach (var cde in checksDE)
                        {
                            dbContext.ChecksDataEntry.Add(cde);
                        }

                        //lblTrans.Text = dbContext.Database.Log.ToString();
                    }
                    dbContext.SaveChanges();
                }
            }
            catch
            {
                throw;
            }
        }

        private Batch CreateBatch(IEnumerable<BankTran> bankTrans)
        {
            int globalBatchID;

            using (var dbContext = new ItemDBContext())
            {
                // Key is equal to the current max value plus one
                globalBatchID = dbContext.Batch.Max(b => b.GlobalBatchID) + 1;
            }

            double amount;
            double totalAmt = 0;
            foreach (var tran in bankTrans)
            {
                if (double.TryParse(tran.PaymentAmount, out amount))
                {
                    totalAmt += amount;
                }
                else throw new Exception("Unable to parse transaction amount: " + tran.PaymentAmount);
            }

            DateTime curDate = DateTime.Now;
            Batch batch = new Batch()
            {
                GlobalBatchID = globalBatchID,
                BankID = 7,
                LockboxID = bankTrans.Any(x => x.RecoveryPaymentType == "OVP")? 316 : 306, // overpaid batch or BRI batch
                DESetupID = null,
                ProcessingDate = curDate.Date,
                BatchID = 203,
                DepositStatus = 850,
                BatchCueID = 0,// ???,
                CutOffStatus = 0,
                BatchMode = null,
                SystemType = 0,
                Priority = 1,
                PrintLabels = 1,
                BatchTypeCode = 50, // ???,
                BatchTypeSubCode = 0,
                BatchTypeText = null,
                ConsolidationStatus = 0,
                CARStatus = null,
                KFIStatus = null,
                FAXStatus = null,
                FloatAssignmentStatus = 0,
                ScanStatus = 21,
                DEStatus = 0,
                ExtractSequenceNumber = 0,
                ImageExtractSequenceNumber = null,
                DepositDate = curDate.Date,
                CheckCount = bankTrans.Count(),
                CheckAmount = (decimal)totalAmt,
                StubCount = bankTrans.Count(),
                StubAmount = (decimal)totalAmt,
                CreationDate = curDate,
                BillingTC = 0,
                BillingRT = 0,
                BillingAccount = 0,
                BillingSerial = 0,
                WorkgroupID = 1,
                CurrencyCode = null,
                MICRVerifyStatus = 4,
                DEPrinted = null,
                Is2Pass = 1,
                UseCar = 1,
                NoChecksEnclosed = 0,
                Rejects = 0,
                DepositTicketPrinted = false,
                DateLastUpdated = curDate,
                TransportID = null,
                TrayID = null,
                SubTrayID = null,
                UseCustomer = 0,
                DepositDDA = null,
                EncodeComplete = null,
                SplitsEnded = false,
                SiteCode = null,
                BitonalImageRemovalDate = null, // ist seems this removal dates are a 1 1/2 month later,
                DataRemovalDate = null,
                ModificationDate = curDate,
                TransportBatchID = null,
                GrayScaleImageRemovalDate = null,
                ColorImageRemovalDate = null,
                CWDBDataRemovalDate = null,
                CWDBBitonalImageRemovalDate = null,
                CWDBGrayScaleImageRemovalDate = null,
                CWDBColorImageRemovalDate = null,
                BatchName = null,
                BatchTypeID = Guid.NewGuid(),
                DecisionStatus = 0,
                AllowDecisioning = false,
                RequireDEBeforeTwoPass = false,
                OriginalGlobalBatchID = null,
                BatchExtractID = null,
                IsImageExchange = false,
                CaptureSiteCodeID = 10,
                BatchSourceKey = 1,
                NumOLDDaysRolled = 0,
            };
            return batch;
        }
        private BatchDataEntry CreateBatchDataEntry(int globalBatchID)
        {
            BatchDataEntry bde = new BatchDataEntry()
            {
                GlobalBatchID = globalBatchID,
                ChecksStorageBoxNumber = null,
                DocumentsStorageBoxNumber = null,
                IronMountainBoxNumber = null
            };

            return bde;
        }

        private IEnumerable<BankTran> FilterTransactions(PaymentHandler filter, IEnumerable<BankTran> trans)
        {
            foreach (var item in trans)
            {
                if (filter(item.RecoveryPaymentType))
                {
                    yield return item;
                }
            }
        }

        private IEnumerable<BankTran> BankTrans(IEnumerable<dynamic> transactions)
        {
            foreach (var tran in transactions)
            {
                BankTran bankTran = new BankTran()
                {
                    UserID = tran.UserID,
                    AmountDue = tran.AmountDue,
                    PaymentAmount = tran.PaymentAmount,
                    DueDate = tran.DueDate,
                    FirstName = tran.FirstName,
                    LastName = tran.LastName
                };


                foreach (var par in tran.Parameter)
                {
                    if (par.Element("Name").Value == "arpID")
                        bankTran.ArpID = par.Element("Value").Value;

                    if (par.Element("Name").Value == "recoveryPaymentType")
                        bankTran.RecoveryPaymentType = par.Element("Value").Value;
                }

                yield return bankTran;
            }
        }

        private IEnumerable<Transactions> CreateTransactions(IEnumerable<Checks> checks, Batch batch)
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

        private IEnumerable<Checks> CreateChecks(IEnumerable<BankTran> transactions, Batch batch)
        {
            int globalCheckID = CreateCheckID();

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
                    KilledMethod = 6,
                    RemitterName = tran.FirstName + " " + tran.LastName
                };

                yield return check;
            }
        }

        private IEnumerable<ChecksDataEntry> CreateChecksDataEntry(IEnumerable<Checks> checks, Batch batch, IEnumerable<BankTran> transactions)
        {
            var checksAndTrans = checks.Zip(transactions, (check, tran) => new { Checks = check, BankTran = tran });
            foreach (var ct in checksAndTrans)
            {
                ChecksDataEntry cde = new ChecksDataEntry()
                {
                    GlobalBatchID = batch.GlobalBatchID,
                    GlobalCheckID = ct.Checks.GlobalCheckID,
                    TransactionID = ct.Checks.TransactionID,
                    EntityID = ct.BankTran.ArpID,                    
                };

                yield return cde;
            }
        }

        private static int CreateCheckID()
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
    }
}