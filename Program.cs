using SealScout;

namespace SealLead
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            DatabaseService.Initialize();
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}

