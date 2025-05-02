using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autoupdater
{
    public interface IUpdateSource
    {
        Task<UpdateInfo> CheckForUpdateAsync(string currentVersion);
        Task DownloadLatestAsync(string destinationPath);
    }
}
