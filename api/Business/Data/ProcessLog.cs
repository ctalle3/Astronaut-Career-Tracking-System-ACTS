using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace StargateAPI.Business.Data
{
    [Table("ProcessLog")]
    public class ProcessLog
    {
        public int Id { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        // "Information" for successes, "Error" for exceptions
        public string Level { get; set; } = string.Empty;

        // The MediatR request type, e.g. "CreateAstronautDuty"
        public string RequestName { get; set; } = string.Empty;

        // The request payload serialized as JSON
        public string? RequestData { get; set; }

        public string Message { get; set; } = string.Empty;

        // Full exception text (type, message, stack trace); null on success
        public string? Exception { get; set; }
    }

    public class ProcessLogConfiguration : IEntityTypeConfiguration<ProcessLog>
    {
        public void Configure(EntityTypeBuilder<ProcessLog> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.Level).IsRequired();
            builder.Property(x => x.RequestName).IsRequired();
            builder.Property(x => x.Message).IsRequired();
        }
    }
}
