namespace ImageConverter.Core.Models;

public sealed record ExportProgress(int Completed, int Total, string CurrentFile, ExportResult? Result)
{
    public double Percentage
    {
        get
        {
            if (Total != 0)
            {
                return (double)Completed * 100.0 / (double)Total;
            }
            return 0.0;
        }
    }
}
