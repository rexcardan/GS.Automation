using DICOMAnon.Exporter.Models;
using DAAPI.Models;

namespace DICOMAnon.Exporter.Services
{
    public interface IWorklistBuilder
    {
        WorklistItem Build(SelectionResult selection);
    }
}

