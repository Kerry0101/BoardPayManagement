using System.Text;
using BoardPaySystem.Models;

namespace BoardPaySystem.Services
{
    public static class SmsMessageTemplates
    {
        /// <summary>
        /// Creates a message for bill approval notification
        /// </summary>
        public static string BillApproved(Bill bill, string tenantName)
        {
            if (bill == null)
                throw new ArgumentNullException(nameof(bill));

            var billingPeriod = new DateTime(bill.BillingYear, bill.BillingMonth, 1)
                .ToString("MMMM yyyy");

            return $"Hello {tenantName}, your bill for {billingPeriod} has been approved. Amount: {bill.TotalAmount.ToString("C", new System.Globalization.CultureInfo("en-PH"))}. Due date: {bill.DueDate:MM/dd/yyyy}. Please login to your account for details.";
        }

        /// <summary>
        /// Creates a message for payment confirmation
        /// </summary>
        public static string PaymentConfirmed(Bill bill, string tenantName, decimal amount)
        {
            if (bill == null)
                throw new ArgumentNullException(nameof(bill));

            var billingPeriod = new DateTime(bill.BillingYear, bill.BillingMonth, 1)
                .ToString("MMMM yyyy");

            return $"Thank you, {tenantName}. Your payment of {amount.ToString("C", new System.Globalization.CultureInfo("en-PH"))} for {billingPeriod} has been received and confirmed. Receipt available in your account.";
        }

        /// <summary>
        /// Creates a message for due date reminder (3 days before due date)
        /// </summary>
        public static string PaymentDueReminder(Bill bill, string tenantName)
        {
            if (bill == null)
                throw new ArgumentNullException(nameof(bill));

            var billingPeriod = new DateTime(bill.BillingYear, bill.BillingMonth, 1)
                .ToString("MMMM yyyy");

            return $"Reminder: {tenantName}, your payment of {bill.TotalAmount.ToString("C", new System.Globalization.CultureInfo("en-PH"))} for {billingPeriod} is due on {bill.DueDate:MM/dd/yyyy}. Please login to make your payment.";
        }

        /// <summary>
        /// Creates a message for overdue payment
        /// </summary>
        public static string PaymentOverdue(Bill bill, string tenantName, int daysOverdue, decimal lateFeeAmount)
        {
            if (bill == null)
                throw new ArgumentNullException(nameof(bill));

            var billingPeriod = new DateTime(bill.BillingYear, bill.BillingMonth, 1)
                .ToString("MMMM yyyy");

            var sb = new StringBuilder();
            sb.Append($"IMPORTANT: {tenantName}, your payment for {billingPeriod} is {daysOverdue} days overdue. ");

            if (lateFeeAmount > 0)
            {
                sb.Append($"A late fee of {lateFeeAmount.ToString("C", new System.Globalization.CultureInfo("en-PH"))} has been applied. ");
                sb.Append($"New total: {bill.TotalAmount.ToString("C", new System.Globalization.CultureInfo("en-PH"))}. ");
            }
            else
            {
                sb.Append($"Amount due: {bill.TotalAmount.ToString("C", new System.Globalization.CultureInfo("en-PH"))}. ");
            }

            sb.Append("Please settle your account immediately.");

            return sb.ToString();
        }
    }
}
