using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VrchatgroupApp.clean.Services;

public interface ISessionStore
{
    Task SaveAsync(StoredSession session);
    Task<StoredSession?> LoadAsync();
    Task ClearAsync();
}


