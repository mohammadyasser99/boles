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

    public UserService(IUserRepository userRepository , ICarRepository carRepository ,IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _carRepository = carRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository
            .GetAll()
            .Select(u => new UserDto(u.Id, u.Name, u.PhoneNumber, u.Email))
            .ToListAsync();

        return users;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? null : new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email);
    }

    public async Task<UserDto> CreateUserWithCarAsync(CreateUserWithCarDto dto)
    {
        var user = new User
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
            UserId = user.Id
        };
        await _carRepository.AddAsync(car);

        await _unitOfWork.SaveChangesAsync();
        return new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email);
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
            //first get the user
            User user = await _userRepository.GetByIdAsync(dto.UserId) ?? throw new KeyNotFoundException("user doesnt exist");

            //find the car
            Car car = await _carRepository.GetAll().Where(x => x.CarPlate == dto.CarPlate).FirstOrDefaultAsync();
            if (car==null)
            {

                throw new Exception("the car doesnt exist");

            }
            user.DateOfPayment = dto.DateOfPayment;
            user.Name=dto.Name;
            user.PhoneNumber =dto.PhoneNumber;
            user.NationalId = dto.NationalId;
            user.Email=dto.Email;
            
            car.UserId = dto.UserId;
            car.ChassisNumber = dto.ChassisNumber;
            car.CarPlate = dto.CarPlate;
            car.RentalPrice = dto.RentalPrice;
            car.Brand = dto.Brand;
            car.Model = dto.Model;
            car.Year = dto.Year;

            await _unitOfWork.SaveChangesAsync();

        }
        catch (Exception ex)
        {
        }
    }
}
