using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Automation;

namespace FastDelete.ShellExtension
{
    public partial class FastDeleteForm : Form
    {
        public FastDeleteForm()
        {
            InitializeComponent();
        }

        private Thread deleteThread = null;
        private readonly CancellationTokenSource deleteCancelToken = new CancellationTokenSource();
        public DirectoryInfo DirectoryToDelete { get; set; }

        private void FastDeleteForm_Load(object sender, EventArgs e)
        {
            if (DirectoryToDelete == null)
            {
                throw new InvalidOperationException("DirectoryToDelete must be set before the form is shown");
            }

            mInstructionLabel.Text = $"Are you sure you want to delete \"{DirectoryToDelete.Name}\"?";
        }

        private void mCancelButton_Click(object sender, EventArgs e)
        {
            deleteCancelToken.Cancel();
            deleteThread.Join();

            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void mConfirmButton_Click(object sender, EventArgs e)
        {
            mConfirmButton.Visible = false;
            mInstructionLabel.Visible = false;
            mProgressBar.Visible = true;

            deleteThread = new Thread(DeleteThreadProc);
            deleteThread.SetApartmentState(ApartmentState.STA);
            deleteThread.Start(this);
        }

        private static void DeleteThreadProc(object param)
        {
            FastDeleteForm form = (FastDeleteForm)param;
            var token = form.deleteCancelToken.Token;

            try
            {
                token.ThrowIfCancellationRequested();
                form.DirectoryToDelete.Delete(true);
                form.Invoke((Action)delegate { form.DeletionSuccess(); });
            }
            catch (OperationCanceledException)
            {
                // Swallow this - don't display an error
            }
            catch (Exception ex)
            {
                form.Invoke((Action)delegate { form.DeletionException(ex); });
            }
        }

        private void DeletionSuccess()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DeletionException(Exception ex)
        {
            string message;
            if (ex is UnauthorizedAccessException) message = $"{DirectoryToDelete.Name} contains a read-only file.";
            else if (ex is DirectoryNotFoundException) message = $"{DirectoryToDelete.Name} was not found.";
            else if (ex is IOException) message = $"{DirectoryToDelete.Name} is read-only, or an I/O error occurred.";
            else message = $"Unhandled exception during deletion: {ex.Message}";

            MessageBox.Show(this, message, "FastDelete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            mConfirmButton.Visible = true;
            mInstructionLabel.Visible = true;
            mProgressBar.Visible = false;
        }
    }
}
