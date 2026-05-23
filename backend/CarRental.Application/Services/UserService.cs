using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace CarRental.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ICarRepository _carRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserDocumentService _documentService;

    public UserService(IUserRepository userRepository , ICarRepository carRepository ,IUnitOfWork unitOfWork,IUserDocumentService userDocumentService)
    {
        _userRepository = userRepository;
        _carRepository = carRepository;
        _unitOfWork = unitOfWork;
        _documentService = userDocumentService;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        try
        {
            var users = await _userRepository
            .GetAll()
            .Include(x => x.Documents)
            .Select(u => new UserDto(
                u.Id,
                u.Name,
                u.PhoneNumber,
                u.Email,
                u.NationalId,
                u.DateOfPayment,
                u.JoinDate,

                u.Documents.Select(d => new UserDocumentDto(
                    d.Id,
                    d.ClientId,
                    d.DocumentType.ToString(),
                    d.FileName,
                    d.ContentType,
                    d.FileSizeBytes,
                    d.UploadedAt
                )).ToList()
                ,
                u.ContractExpiry
            ))
            .ToListAsync();

            return users;
        }
        catch (Exception ex)
        {
            return null;
        }
        
    }
    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? null : new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email,user.NationalId ,user.DateOfPayment , user.JoinDate,null,user.ContractExpiry);
    }

    public async Task<UserDto> CreateUserWithCarAsync(CreateUserWithCarDto dto)
    {
        // ✅ FIX: was inverted — throw when user ALREADY EXISTS
        var existing = await _userRepository.GetAll()
            .Where(x => x.NationalId == dto.NationalId)
            .FirstOrDefaultAsync();

        if (existing != null)
            throw new Exception("A user with this National ID already exists.");

        // ✅ FIX: validate dates before creating anything
        if (dto.JoinDate >= dto.ContractExpiry)
            throw new Exception("Join date must be before contract expiry.");

        // ── Build payment schedule ──────────────────────────────────────────
        var schedule = GeneratePaymentSchedule(dto.JoinDate, dto.ContractExpiry, dto.MonthlyAmounts);

        // ── Create client ───────────────────────────────────────────────────
        var user = new Client
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            NationalId = dto.NationalId,
            DateOfPayment = dto.DateOfPayment,
            JoinDate = dto.JoinDate,        // ✅ FIX: was missing
            ContractExpiry = dto.ContractExpiry,
            PaymentScheduleJson = JsonSerializer.Serialize(schedule)
        };
        await _userRepository.AddAsync(user);

        // ── Create car ──────────────────────────────────────────────────────
        var existingCar = await _carRepository.GetAll()
            .Where(x => x.CarPlate == dto.CarPlate)
            .FirstOrDefaultAsync();

        if (existingCar != null)
            throw new Exception("A car with this plate already exists.");

        var car = new Car
        {
            CarPlate = dto.CarPlate,
            Brand = dto.Brand,
            Model = dto.Model,
            Year = dto.Year,
            ChassisNumber = dto.ChassisNumber,
            ClientId = user.Id
        };
        await _carRepository.AddAsync(car);

        // ── Upload documents ────────────────────────────────────────────────
        if (dto.DocumentFiles != null &&
            dto.DocumentTypes != null &&
            dto.DocumentFiles.Count == dto.DocumentTypes.Count)
        {
            for (int i = 0; i < dto.DocumentFiles.Count; i++)
                await _documentService.UploadDocumentAsync(user.Id, dto.DocumentFiles[i], dto.DocumentTypes[i]);
        }

        await _unitOfWork.SaveChangesAsync();

        return new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email,
                           user.NationalId, user.DateOfPayment, user.JoinDate,
                           null, user.ContractExpiry);
    }

    public async Task ModifyUserAndCar(CreateUserWithCarDto dto)
    {
        // ── Get user ─────────────────────────────
        var user = await _userRepository.GetByIdAsync(dto.UserId)
            ?? throw new KeyNotFoundException("User doesn't exist");

        // ── Check National ID uniqueness ────────
        var nationalIdExists = await _userRepository.GetAll()
            .AnyAsync(x =>
                x.NationalId == dto.NationalId &&
                x.Id != dto.UserId);

        if (nationalIdExists)
        {
            throw new Exception("The national ID already exists");
        }

        // ── Get current user car ─────────────────
        var car = await _carRepository.GetAll()
            .FirstOrDefaultAsync(x => x.ClientId == dto.UserId);

        // ── Check Car Plate uniqueness ───────────
        var carPlateExists = await _carRepository.GetAll()
            .AnyAsync(x =>
                x.CarPlate == dto.CarPlate &&
                x.ClientId != dto.UserId);

        if (carPlateExists)
        {
            throw new Exception("The car plate already exists");
        }

        // ── CREATE or UPDATE CAR ─────────────────
        if (car == null)
        {
            car = new Car
            {
                CarPlate = dto.CarPlate,
                Brand = dto.Brand,
                Model = dto.Model,
                ChassisNumber = dto.ChassisNumber,
                Year = dto.Year,
                ClientId = dto.UserId
            };

            await _carRepository.AddAsync(car);
        }
        else
        {
            car.ClientId = dto.UserId;
            car.ChassisNumber = dto.ChassisNumber;
            car.CarPlate = dto.CarPlate;
            car.Brand = dto.Brand;
            car.Model = dto.Model;
            car.Year = dto.Year;
        }

        // ── UPDATE PAYMENT SCHEDULE ─────────────────────────
        if (dto.MonthlyAmounts != null && dto.MonthlyAmounts.Count > 0)
        {
            // existing schedule from DB
            var existingSchedule = new List<PaymentScheduleItem>();

            if (!string.IsNullOrWhiteSpace(user.PaymentScheduleJson))
            {
                existingSchedule =
                    JsonSerializer.Deserialize<List<PaymentScheduleItem>>(
                        user.PaymentScheduleJson
                    ) ?? new List<PaymentScheduleItem>();
            }

            // generate months between join and expiry
            var monthSlots = new List<(int Year, int Month)>();

            var cursor = new DateOnly(dto.JoinDate.Year, dto.JoinDate.Month, 1);
            var end = new DateOnly(dto.ContractExpiry.Year, dto.ContractExpiry.Month, 1);

            while (cursor <= end)
            {
                monthSlots.Add((cursor.Year, cursor.Month));
                cursor = cursor.AddMonths(1);
            }

            var updatedSchedule = new List<PaymentScheduleItem>();

            for (int i = 0; i < monthSlots.Count; i++)
            {
                var slot = monthSlots[i];

                // existing month
                var existing = existingSchedule.FirstOrDefault(x =>
                    x.Year == slot.Year &&
                    x.Month == slot.Month);

                if (existing != null)
                {
                    var newAmount =
                        i < dto.MonthlyAmounts.Count
                            ? dto.MonthlyAmounts[i]
                            : existing.Amount;

                    // prevent lowering below already paid amount
                    if (newAmount < existing.RentalPaid)
                    {
                        throw new Exception(
                            $"Cannot set rental price for {slot.Month}/{slot.Year} to {newAmount} because the client already paid {existing.RentalPaid}"
                        );
                    }

                    // update only rental price
                    existing.Amount = newAmount;

                    // auto update paid status
                    existing.IsPaid = existing.RentalPaid >= existing.Amount;

                    updatedSchedule.Add(existing);
                }
                else
                {
                    // new month
                    updatedSchedule.Add(new PaymentScheduleItem
                    {
                        Year = slot.Year,
                        Month = slot.Month,
                        Amount = i < dto.MonthlyAmounts.Count
                            ? dto.MonthlyAmounts[i]
                            : 0,

                        RentalPaid = 0,
                        IsPaid = false,
                        PaidAt = null
                    });
                }
            }

            user.PaymentScheduleJson =
                JsonSerializer.Serialize(updatedSchedule);
        }

        // ── UPDATE USER ──────────────────────────
        user.Name = dto.Name;
        user.PhoneNumber = dto.PhoneNumber;
        user.NationalId = dto.NationalId;
        user.Email = dto.Email;
        user.DateOfPayment = dto.DateOfPayment;
        user.JoinDate = dto.JoinDate;
        user.ContractExpiry = dto.ContractExpiry;

        // ── DOCUMENT SYNC ────────────────────────
        var existingDocs =
            (await _documentService.GetUserDocumentsAsync(dto.UserId))
            .ToList();

        var keptIds = dto.ExistingDocumentIds?.ToHashSet()
                      ?? new HashSet<Guid>();

        foreach (var doc in existingDocs)
        {
            if (!keptIds.Contains(doc.Id))
            {
                await _documentService.DeleteDocumentAsync(doc.Id);
            }
        }

        if (dto.DocumentFiles != null &&
            dto.DocumentTypes != null &&
            dto.DocumentFiles.Count == dto.DocumentTypes.Count)
        {
            for (int i = 0; i < dto.DocumentFiles.Count; i++)
            {
                await _documentService.UploadDocumentAsync(
                    dto.UserId,
                    dto.DocumentFiles[i],
                    dto.DocumentTypes[i]
                );
            }
        }

        // ── SAVE ─────────────────────────────────
        await _unitOfWork.SaveChangesAsync();
    }
    public async Task<UserDto> CreateUserWithOptionalDocumentAsync(CreateUserWithOptionalDocumentDto dto)
    {
        var existingNational = await _userRepository.GetAll().Where(x => x.NationalId == dto.NationalId).AnyAsync();
        if (existingNational )
        {
            throw new Exception("the national id already exist");
        }

        var user = new Client
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                PhoneNumber = dto.PhoneNumber,
                Email = dto.Email,
                NationalId = dto.NationalId,
                DateOfPayment = dto.DateOfPayment,
                JoinDate = dto.JoinDate,
                ContractExpiry = dto.ContractExpiry
            };

            await _userRepository.AddAsync(user);


            // ✅ OPTIONAL DOCUMENT
            if (dto.DocumentFiles != null &&
                dto.DocumentTypes != null &&
                dto.DocumentFiles.Count == dto.DocumentTypes.Count)
            {
                for (int i = 0; i < dto.DocumentFiles.Count; i++)
                {
                    await _documentService.UploadDocumentAsync(
                        user.Id,
                        dto.DocumentFiles[i],
                        dto.DocumentTypes[i]
                    );
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email, user.NationalId, user.DateOfPayment, user.JoinDate, null,user.ContractExpiry);
        
    

    }

    public async Task<UserDto> UpdateUserWithDocumentAsync(Guid id, UpdateUserWithDocumentDto dto)
    {
        if (dto.JoinDate > dto.ContractExpiry)
        {
            throw new Exception("the join date must be before contract expiry");
        }

            var user = await _userRepository.GetByIdAsync(id)
    ?? throw new KeyNotFoundException($"User '{id}' not found.");

        var existingNational = await _userRepository.GetAll().Where(x => x.NationalId == dto.NationalId).AnyAsync();
        if (existingNational && user.NationalId !=dto.NationalId)
        {
            throw new Exception("the national id already exist");
        }

        // ── Update user fields ─────────────────────────────
        user.Name = dto.Name;
            user.Email = dto.Email;
            user.PhoneNumber = dto.PhoneNumber;
            user.NationalId = dto.NationalId;
             user.JoinDate = dto.JoinDate;
            user.DateOfPayment = dto.DateOfPayment;
            user.ContractExpiry = dto.ContractExpiry;
            await _userRepository.UpdateAsync(user);

            // ── Get existing documents ─────────────────────────
            var existingDocs = (await _documentService.GetUserDocumentsAsync(id)).ToList();

            var keptIds = (dto.ExistingDocumentIds ?? new List<Guid>())
                .ToHashSet();

            // ── Delete removed documents ───────────────────────
            foreach (var doc in existingDocs)
            {
                if (!keptIds.Contains(doc.Id))
                {
                    await _documentService.DeleteDocumentAsync(doc.Id);
                }
            }

            // ── Upload new documents ───────────────────────────
            if (dto.DocumentFiles != null &&
                dto.DocumentTypes != null &&
                dto.DocumentFiles.Count == dto.DocumentTypes.Count)
            {
                for (int i = 0; i < dto.DocumentFiles.Count; i++)
                {
                    await _documentService.UploadDocumentAsync(
                        id,
                        dto.DocumentFiles[i],
                        dto.DocumentTypes[i]
                    );
                }
            }

            await _unitOfWork.SaveChangesAsync(); // ✅ FIXED

            return new UserDto(
                Id: user.Id,
                Name: user.Name,
                PhoneNumber: user.PhoneNumber,
                Email: user.Email,
                NationalId: user.NationalId,
                DateOfPayment: user.DateOfPayment,
                JoinDate: user.JoinDate,
                null,
                ContractExpiry:user.ContractExpiry
            );
        
   
         
    

    }
    public async Task<UserWithCarDto?> GetUserWithCarAsync(Guid userId)
    {
        var user = await _userRepository
            .GetAll()
            .Include(u => u.Documents)
            .Where(u => u.Id == userId)
            .Select(u => new UserWithCarDto(
                u.Id,
                u.Name,
                u.PhoneNumber,
                u.Email,
                u.NationalId,
                u.DateOfPayment,
                u.JoinDate,
                u.ContractExpiry,
                u.PaymentScheduleJson,
                u.Cars
                    .Select(c => new CarDtoo(
                        c.CarPlate,
                        c.Brand,
                        c.Model,
                        c.Year,
                        c.ChassisNumber
                    ))
                    .FirstOrDefault(),

                u.Documents.Select(d => new UserDocumentDto(
                    d.Id,
                    d.ClientId,
                    d.DocumentType.ToString(),
                    d.FileName,
                    d.ContentType,
                    d.FileSizeBytes,
                    d.UploadedAt
                )).ToList()
            ))
            .FirstOrDefaultAsync();

        return user;
    }

    public async Task MarkPaymentAsPaidAsync(Guid clientId, int month, int year)
    {
        var client = await _userRepository.GetAll()
            .FirstOrDefaultAsync(x => x.Id == clientId)
            ?? throw new Exception("Client not found.");

        var schedule = string.IsNullOrEmpty(client.PaymentScheduleJson)
            ? new List<PaymentScheduleItem>()
            : JsonSerializer.Deserialize<List<PaymentScheduleItem>>(client.PaymentScheduleJson)!;

        var entry = schedule.FirstOrDefault(p => p.Month == month && p.Year == year)
            ?? throw new Exception($"No payment entry found for {month}/{year}.");

        if (entry.IsPaid)
            throw new Exception("This month is already marked as paid.");

        entry.IsPaid = true;
        entry.PaidAt = DateTime.UtcNow;

        client.PaymentScheduleJson = JsonSerializer.Serialize(schedule);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── Helper ───────────────────────────────────────────────────────────────
    private static List<PaymentScheduleItem> GeneratePaymentSchedule(
        DateOnly joinDate,
        DateOnly contractExpiry,
        List<decimal>? amounts)
    {
        var schedule = new List<PaymentScheduleItem>();
        var current = new DateOnly(joinDate.Year, joinDate.Month, 1);
        var end = new DateOnly(contractExpiry.Year, contractExpiry.Month, 1);
        int index = 0;

        while (current <= end)
        {
            schedule.Add(new PaymentScheduleItem
            {
                Month = current.Month,
                Year = current.Year,
                Amount = amounts != null && index < amounts.Count ? amounts[index] : 0,
                RentalPaid = 0,      // ← only change: explicit zero, no payments yet
                IsPaid = false,
                PaidAt = null
            });
            current = current.AddMonths(1);
            index++;
        }
        return schedule;
    }
}
