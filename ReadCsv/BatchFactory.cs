using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ReadCsv
{
    public class BatchFactory
    {


        public static Batch GetBatch(string batchType, IEnumerable<BankTran> bankTrans)
        {
            if (batchType == "recovery")
            {
                return CreateRecoveryBatch(bankTrans);
            }
            else
                throw new Exception("Batch Type is not recognized");
        }

        private static Batch CreateRecoveryBatch(IEnumerable<BankTran> bankTrans)
        {

            DateTime curDate = DateTime.Now;
            int globalBatchID;
            double amount;
            double totalAmt = 0;
            int lockboxID;
            int bankID;
            int checkCount;
            int stubCount;

            lockboxID = bankTrans.Any(x => x.RecoveryPaymentType == "OVP") ? 316 : 306; // overpaid batch or BRI batch
            bankID = 7;
            checkCount = bankTrans.Count();
            stubCount = bankTrans.Count();

            using (var dbContext = new ItemDBContext())
            {
                // Key is equal to the current max value plus one
                globalBatchID = dbContext.Batch.Max(b => b.GlobalBatchID) + 1;
            }

            foreach (var tran in bankTrans)
            {
                if (double.TryParse(tran.PaymentAmount, out amount))
                {
                    totalAmt += amount;
                }
                else throw new Exception("Unable to parse transaction amount: " + tran.PaymentAmount);
            }
            Batch batch = new Batch()
            {
                GlobalBatchID = globalBatchID,
                BankID = bankID,
                LockboxID = lockboxID,
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
                CheckCount = checkCount,
                CheckAmount = (decimal)totalAmt,
                StubCount = stubCount,
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
    }
}