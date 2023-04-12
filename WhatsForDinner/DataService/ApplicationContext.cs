using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsForDinner.Config;
using WhatsForDinner.DataService.Entities;

namespace WhatsForDinner.DataService
{
    public class ApplicationContext : DbContext
    {
        public DbSet<Dish> Dishes => Set<Dish>();
        public DbSet<Customer> Customers => Set<Customer>();
        public ApplicationContext() => Database.EnsureCreated();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(DinnerConfig.AppConfiguration["WhatsForDinner:ConnectionStrings:DishDBConnectionString"]);
        }
    }
}
