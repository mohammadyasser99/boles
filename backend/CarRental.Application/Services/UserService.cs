using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Interfaces;

namespace CarRental.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(u => new UserDto(u.Id, u.Name, u.PhoneNumber, u.Email));
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? null : new UserDto(user.Id, user.Name, user.PhoneNumber, user.Email);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email
        };
        await _userRepository.AddAsync(user);
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
    }

    public async Task DeleteUserAsync(Guid id)
    {
        _ = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"User {id} not found.");
        await _userRepository.DeleteAsync(id);
    }
}
