using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
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
            ))
            .ToListAsync();

        return users;
    }
    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? null : new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email,user.NationalId ,user.DateOfPayment , user.JoinDate,null);
    }

    public async Task<UserDto> CreateUserWithCarAsync(CreateUserWithCarDto dto)
    {
        var user = new Client
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            NationalId =dto.NationalId,
            DateOfPayment = dto.DateOfPayment
        };
        await _userRepository.AddAsync(user);

        var car = new Car
        {
            CarPlate = dto.CarPlate,
            Brand = dto.Brand,
            Model = dto.Model,
            RentalPrice = dto.RentalPrice,
            ChassisNumber = dto.ChassisNumber,
            ClientId = user.Id
        };
        await _carRepository.AddAsync(car);

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
        return new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email, user.NationalId, user.DateOfPayment, user.JoinDate,null);
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");
        user.Name = dto.Name;
        user.PhoneNumber = dto.PhoneNumber;
        user.Email = dto.Email;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChanges();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        _ = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");
        await _userRepository.DeleteAsync(id);
        await _userRepository.SaveChanges();
    }

    public async Task ModifyUserAndCar(CreateUserWithCarDto dto)
    {
        try
        {
            // ── Get user ─────────────────────────────
            var user = await _userRepository.GetByIdAsync(dto.UserId)
                ?? throw new KeyNotFoundException("user doesnt exist");

            // ── Get car ──────────────────────────────
            var car = await _carRepository.GetAll()
                .FirstOrDefaultAsync(x => x.CarPlate == dto.CarPlate);

            // ── CREATE or UPDATE CAR (UPSERT) ────────
            if (car == null)
            {
                car = new Car
                {
                    CarPlate = dto.CarPlate,
                    Brand = dto.Brand,
                    Model = dto.Model,
                    RentalPrice = dto.RentalPrice,
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
                car.RentalPrice = dto.RentalPrice;
                car.Brand = dto.Brand;
                car.Model = dto.Model;
                car.Year = dto.Year;
            }

            // ── UPDATE USER ──────────────────────────
            user.Name = dto.Name;
            user.PhoneNumber = dto.PhoneNumber;
            user.NationalId = dto.NationalId;
            user.Email = dto.Email;
            user.DateOfPayment = dto.DateOfPayment;
            user.JoinDate = dto.JoinDate;
            // ── DOCUMENT SYNC ────────────────────────
            var existingDocs = (await _documentService.GetUserDocumentsAsync(dto.UserId)).ToList();

            var keptIds = dto.ExistingDocumentIds?.ToHashSet() ?? new HashSet<Guid>();

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

            await _unitOfWork.SaveChangesAsync();
        }
        catch
        {
            throw;
        }
    }
    public async Task<UserDto> CreateUserWithOptionalDocumentAsync(CreateUserWithOptionalDocumentDto dto)
    {
        try
        {
            var user = new Client
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                PhoneNumber = dto.PhoneNumber,
                Email = dto.Email,
                NationalId = dto.NationalId,
                DateOfPayment = dto.DateOfPayment,
                JoinDate = dto.JoinDate
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

            return new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email, user.NationalId, user.DateOfPayment, user.JoinDate, null);
        }
        catch (Exception ex)
        {
            return null;
        }

    }

    public async Task<UserDto> UpdateUserWithDocumentAsync(Guid id, UpdateUserWithDocumentDto dto)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id)
    ?? throw new KeyNotFoundException($"User '{id}' not found.");

            // ── Update user fields ─────────────────────────────
            user.Name = dto.Name;
            user.Email = dto.Email;
            user.PhoneNumber = dto.PhoneNumber;
            user.NationalId = dto.NationalId;

            if (dto.JoinDate.HasValue)
                user.JoinDate = dto.JoinDate.Value;

            user.DateOfPayment = dto.DateOfPayment;

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
                null
            );
        }
        catch (Exception ex)
        {
            return null;
        }

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

                u.Cars
                    .Select(c => new CarDtoo(
                        c.CarPlate,
                        c.Brand,
                        c.Model,
                        c.Year,
                        c.RentalPrice,
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
}
