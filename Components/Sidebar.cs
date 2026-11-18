using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using reportmangerv2.Data;

namespace reportmangerv2.Components;

public class Sidebar:ViewComponent
{
   
        public readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        
        public Sidebar(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            // get current user id
                var userId = _userManager.GetUserId(HttpContext.User);
                if(string.IsNullOrEmpty(userId))
                {
                    return View(new List<CategoryVM>());
                }

            
            string sqlStatement = @"
SELECT CAST(N'Favorites' AS NVARCHAR2(100)) AS CategoryTree,
       1 AS CategoryLevel,
       CAST(N'4cc7a509-68c1-4c70-9ce4-7604814317d9' AS NVARCHAR2(36)) AS CategoryId,
       CAST(N'' AS NVARCHAR2(10)) AS ParentCategoryId,
       r.id AS reportId,
       r.name AS ReportName
FROM rv2_favorites f
JOIN rv2_aspnetusers u ON f.userid = u.id
JOIN rv2_reports r ON r.id = f.reportid
where u.id= :UserId
UNION ALL
SELECT *
FROM (
    SELECT
      LPAD(CAST(N' ' AS NVARCHAR2(10)), 2*(LEVEL-1)) || c.name AS CategoryTree,
      LEVEL AS CategoryLevel,
      c.id AS CategoryId,
      c.ParentCategoryId,
      r.id AS reportId,
      r.name AS ReportName
    FROM RV2_Categories c
    LEFT JOIN RV2_Reports r
      ON r.categoryid = c.id
    START WITH c.ParentCategoryId IS NULL
    CONNECT BY PRIOR c.id = c.ParentCategoryId
    ORDER SIBLINGS BY c.name,r.createddate 
)

";
            var flatCategories =await _context.Database.SqlQueryRaw<CategoryReportHierarchy>(sqlStatement,new OracleParameter("UserId",userId)).ToListAsync();
          
               var  categories = GetCategoryHierarchies(flatCategories);
            
            return View(categories);
        }
        private IList<CategoryVM> GetCategoryHierarchies(IList<CategoryReportHierarchy> flatCategories) {
            var categoryHierarchies = flatCategories.Distinct().ToList();
            IList<CategoryVM> categories = new List<CategoryVM>();
            foreach (var category in flatCategories)
            {
                if (categories.Any(c => c.CategoryId == category.CategoryId))
                {
                    continue;
                }
                categories.Add(new CategoryVM
                {
                    CategoryId= category.CategoryId,
                    CategoryName= category.CategoryTree,
                    ParentCategoryId= category.ParentCategoryId,
                    Reports=flatCategories.Where(c => c.CategoryId == category.CategoryId && !string.IsNullOrWhiteSpace(c.ReportId) ).Select(c => new ReportVM
                    {
                        ReportId = c.ReportId,
                        ReportName = c.ReportName
                    }).ToList()
                  
                  
                });
            }
            foreach (var category in flatCategories)
            {
                if(category.ParentCategoryId !=null)
                {
                    var parentCategory = categories.FirstOrDefault(c => c.CategoryId == category.ParentCategoryId);
                    if(parentCategory != null)
                    {
                      if(parentCategory.ChildCategories.Any(c=>c.CategoryId == category.CategoryId))
                        {
                            continue;
                        }
                        var childCategory = categories.FirstOrDefault(x => x.CategoryId == category.CategoryId);
                        if (childCategory != null)
                        {
                            parentCategory.ChildCategories.Add(childCategory);
                            
                        }
                    }
                
                }
            }

            var rootCategories = categories.Where(c => c.ParentCategoryId == null).ToList();
            return rootCategories;

}
}

internal class ReportVM
{
   public string ReportDescription { get; set; } = string.Empty;
    public string? ReportId { get; set; }
    public string? ReportName { get; set; }
}

internal class CategoryVM
{
      public string CategoryId { get; set; }
  public string CategoryName { get; set; }
  public string? ParentCategoryId { get; set; }=null;
  public IList<CategoryVM> ChildCategories { get; set; }=new List<CategoryVM>();
  public IList<ReportVM> Reports { get; set; } =new List<ReportVM>();
}

internal class CategoryReportHierarchy
{
    public required string CategoryId { get; set; }
    public string? ParentCategoryId { get; set; }
    public required string CategoryTree { get; set; }
    public int CategoryLevel { get; set; }
    public string? ReportId { get; set; }
    public string? ReportName { get; set; } = string.Empty;

    public CategoryReportHierarchy() { }
    //override equals and gethashcode to compare on Id only
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        CategoryReportHierarchy other = (CategoryReportHierarchy)obj;
        return CategoryId == other.CategoryId;
    }
    public override int GetHashCode()
    {
        return CategoryId.GetHashCode();
    }

}