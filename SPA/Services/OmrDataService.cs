using SPA.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SPA.Models;

namespace SPA.Services
{
    public class OmrDataService
    {
        private readonly FirstDbContext _FirstDbcontext;
        private readonly SecondDbContext _SecondDbContext;
        private readonly DatabaseConnectionChecker _connectionChecker;

        public OmrDataService(FirstDbContext FirstDbcontext, SecondDbContext secondDbContext, DatabaseConnectionChecker connectionChecker)
        {
            _FirstDbcontext = FirstDbcontext;
            _SecondDbContext = secondDbContext;
            _connectionChecker = connectionChecker;
        }

        public async Task<List<OMRdata>> GetOmrDataListAsync(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbcontext.OMRdatas.Where(i => i.ProjectId == ProjectId).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.OMRdatas.Where(i => i.ProjectId == ProjectId).ToListAsync();
            }

        }

        public async Task<List<OMRdata>> GetOmrDataListWithStatus1Async(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbcontext.OMRdatas.Where(i => i.ProjectId == ProjectId && i.Status ==1).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.OMRdatas.Where(i => i.ProjectId == ProjectId).ToListAsync();
            }

        }

        public async Task<List<AmbiguousQue>> GetAmbiguousQuesAsync(string WhichDatabase, int ProjectId, string CourseName)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbcontext.AmbiguousQues.Where(i => i.ProjectId == ProjectId && i.CourseName == CourseName && (i.MarkingId == 5 || i.MarkingId == 4)).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.AmbiguousQues.Where(i => i.ProjectId == ProjectId && i.CourseName == CourseName && (i.MarkingId == 5 || i.MarkingId == 4)).ToListAsync();
            }

        }

        public async Task<List<ResponseConfig>> GetResponseConfigAsync(string WhichDatabase, int ProjectId, string CourseName)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbcontext.ResponseConfigs.Where(i => i.ProjectId == ProjectId && i.CourseName == CourseName).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.ResponseConfigs.Where(i => i.ProjectId == ProjectId && i.CourseName == CourseName).ToListAsync();
            }

        }
        public async Task<List<ExtractedOMRData>> GetExtractedOmrDataListAsync(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbcontext.ExtractedOMRDatas.Where(i => i.ProjectId == ProjectId).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.ExtractedOMRDatas.Where(i => i.ProjectId == ProjectId).ToListAsync();
            }
        }

        public async Task<List<CorrectedOMRData>> GetCorrectedOmrDataListAsync(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                return await _FirstDbcontext.CorrectedOMRDatas.Where(i => i.ProjectId == ProjectId).ToListAsync();
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                return await _SecondDbContext.CorrectedOMRDatas.Where(i => i.ProjectId == ProjectId).ToListAsync();
            }
        }

        public async Task<List<string>> GetAbsenteeListAsync(string WhichDatabase, int ProjectId)
        {
            if (WhichDatabase == "Local")
            {
                var absentees = _FirstDbcontext.Absentees.Where(a => a.ProjectID == ProjectId).Select(u => u.RollNo).ToList();

                return absentees;
            }
            else
            {
                if (!await _connectionChecker.IsOnlineDatabaseAvailableAsync())
                {
                    throw new Exception("Connection Unavailable");
                }
                var absentees = _SecondDbContext.Absentees.Where(a => a.ProjectID == ProjectId).Select(u => u.RollNo).ToList();
                return absentees;
            }



        }

    }
}
