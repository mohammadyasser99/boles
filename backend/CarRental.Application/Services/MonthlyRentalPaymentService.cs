using CarRental.Application.Common;
using CarRental.Application.DTOs;
using CarRental.Application.Interfaces;
using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using CarRental.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarRental.Application.Services
{
    public class MonthlyRentalPaymentService : IMonthlyRentalPaymentService
    {
        private readonly IGenericRepository<Payment> _paymentRepository;
        private readonly IGenericRepository<Client> _userRepository;
        private readonly IGenericRepository<Car> _carRepository;
        private readonly IGenericRepository<Fine> _fineRepository;
        private readonly IGenericRepository<EntranceFee> _entrancefeeRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MonthlyRentalPaymentService> _logger;

        public MonthlyRentalPaymentService(
            IGenericRepository<Payment> paymentRepository,
            IGenericRepository<Client> userRepository,
            IGenericRepository<Car> carRepository,
            IGenericRepository<EntranceFee> entrancefeerepository,
            IGenericRepository<Fine> finerepository,
            IUnitOfWork unitOfWork,
            ILogger<MonthlyRentalPaymentService> logger)
        {
            _paymentRepository = paymentRepository;
            _userRepository = userRepository;
            _carRepository = carRepository;
            _fineRepository = finerepository;
            _entrancefeeRepository = entrancefeerepository;
            _unitOfWork = unitOfWork;
            _logger = logger;

            _logger.LogInformation("MonthlyRentalPaymentService initialized");
        }

        public async Task<ApiResponse<CreateMonthlyRentalPaymentResponseDtos>> CreateAsync(
            CreateMonthlyRentalPaymentRequestDtos request)
        {
            if (request == null)
            {
                _logger.LogError("CreateAsync failed: Request is null");
                return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail("Request cannot be null.");
            }

            _logger.LogInformation("Creating payment for UserId: {UserId}, CarPlate: {CarPlate}, PaymentType: {PaymentType}, Amount: {Amount}",
                request.UserId, request.CarPlate, request.PaymentType, request.Amount);

            try
            {
                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    _logger.LogError("User not found: {UserId}", request.UserId);
                    return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail("User not found.");
                }

                _logger.LogDebug("User found: {UserId}, Name: {UserName}", user.Id, user.Name);

                var car = await _carRepository
                    .GetAll()
                    .FirstOrDefaultAsync(c => c.CarPlate == request.CarPlate);

                if (car == null)
                {
                    _logger.LogError("Car not found with plate: {CarPlate}", request.CarPlate);
                    return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail($"Car with plate '{request.CarPlate}' not found.");
                }

                _logger.LogDebug("Car found: {CarPlate}, Brand: {Brand}, Model: {Model}", car.CarPlate, car.Brand, car.Model);

                // ── Balance payment shortcut ──────────────────────────────────────────────
                if (request.UseBalance)
                {
                    if (user.Balance < request.Amount)
                    {
                        return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail(
                            $"Insufficient balance. Available: {user.Balance:F2}, Required: {request.Amount:F2}");
                    }

                    user.Balance -= request.Amount;

                    var balancePayment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        Amount = request.Amount,
                        PaidAt = request.PaidAt,
                        Car = car,
                        User = user,
                        PaymentType = request.PaymentType,
                        ViolationNumber = request.PaymentType == PaymentType.Fines ? request.ViolationNumber : null,
                        TripNumber = request.PaymentType == PaymentType.EntranceFees ? request.TripNumber : null,
                    };

                    await _paymentRepository.AddAsync(balancePayment);
                    await _unitOfWork.SaveChangesAsync();

                    return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>
                        .Ok(new CreateMonthlyRentalPaymentResponseDtos(balancePayment.Id),
                            $"Payment of {request.Amount:F2} deducted from balance. Remaining balance: {user.Balance:F2}");
                }
                // ── End balance payment shortcut ──────────────────────────────────────────


                if (request.PaymentType == PaymentType.MonthlyRental)
                {
                    _logger.LogDebug("Processing MonthlyRental payment");

                    var monthlyPayment = new AddRentalPaymentDto(
                        request.PaidAt.Month,
                        request.PaidAt.Year,
                        request.Amount);

                    string? balanceMessage = await AddRentalPaymentAsync(request.UserId, monthlyPayment);

                    var message = balanceMessage ?? "Monthly rental payment created successfully.";
                    _logger.LogInformation("Monthly rental payment created successfully for UserId: {UserId}, Amount: {Amount}",
                        request.UserId, request.Amount);

                    return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>
                        .Ok(new CreateMonthlyRentalPaymentResponseDtos(Guid.NewGuid()), message);
                }
                else if (request.PaymentType == PaymentType.Fines)
                {
                    _logger.LogDebug("Processing Fine payment for ViolationNumber: {ViolationNumber}", request.ViolationNumber);

                    var fine = await _fineRepository
                        .GetAll()
                        .Where(x => x.ViolationNumber == request.ViolationNumber)
                        .FirstOrDefaultAsync();

                    if (fine == null)
                    {
                        _logger.LogError("Fine not found with ViolationNumber: {ViolationNumber}", request.ViolationNumber);
                        return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail($"Fine with violation number '{request.ViolationNumber}' not found.");
                    }

                    _logger.LogDebug("Fine found: ViolationNumber={ViolationNumber}, Amount={Amount}, PaidAmount={PaidAmount}",
                        fine.ViolationNumber, fine.Amount, fine.PaidAmount);

                    decimal remaining = fine.Amount - (fine.PaidAmount ?? 0);
                    string? balanceMessage = null;
                    decimal amountToApply = request.Amount;

                    if (request.Amount > remaining)
                    {
                        decimal excess = request.Amount - remaining;
                        amountToApply = remaining;

                        user.Balance += excess;
                        balanceMessage = $"Fine fully paid. Excess amount of {excess:F2} EGP has been added to your balance. New balance: {user.Balance:F2} EGP.";
                        _logger.LogInformation("Excess payment for fine: {Excess} added to user balance. New balance: {Balance}", excess, user.Balance);
                    }

                    fine.PaidAmount = (fine.PaidAmount ?? 0) + amountToApply;
                    fine.IsPaid = fine.PaidAmount >= fine.Amount;

                    var finePayment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        Amount = amountToApply,
                        PaidAt = request.ViolationDate.HasValue
                            ? DateOnly.FromDateTime(request.ViolationDate.Value)
                            : request.PaidAt,
                        Car = car,
                        User = user,
                        PaymentType = request.PaymentType,
                        ViolationNumber = request.ViolationNumber,
                    };

                    await _paymentRepository.AddAsync(finePayment);
                    await _unitOfWork.SaveChangesAsync();

                    var message = balanceMessage ?? "Fine payment created successfully.";
                    _logger.LogInformation("Fine payment created successfully: PaymentId={PaymentId}, ViolationNumber={ViolationNumber}, Amount={Amount}",
                        finePayment.Id, request.ViolationNumber, amountToApply);

                    return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>
                        .Ok(new CreateMonthlyRentalPaymentResponseDtos(finePayment.Id), message);
                }
                else if (request.PaymentType == PaymentType.EntranceFees)
                {
                    _logger.LogDebug("Processing EntranceFees payment");

                    // Get all unpaid entrance fees
                    var unpaidEntranceFees = await _entrancefeeRepository
                        .GetAll()
                        .Where(x => x.Car.Client.Id == user.Id && !x.IsPaid)
                        .OrderBy(x => x.TripDate)
                        .ToListAsync();

                    if (!unpaidEntranceFees.Any())
                    {
                        _logger.LogWarning("No unpaid entrance fees found for UserId: {UserId}", user.Id);
                        return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail("No unpaid entrance fees found.");
                    }

                    _logger.LogDebug("Found {Count} unpaid entrance fees for UserId: {UserId}", unpaidEntranceFees.Count, user.Id);

                    decimal remainingRequestAmount = request.Amount;
                    int feesProcessed = 0;
                    decimal totalPaidFromRequest = 0;
                    decimal totalPaidFromBalance = 0;

                    foreach (var entranceFee in unpaidEntranceFees)
                    {
                        decimal remainingFee = entranceFee.Amount - (entranceFee.PaidAmount ?? 0);

                        if (remainingFee <= 0)
                            continue;

                        // Stop if no money at all
                        if (remainingRequestAmount <= 0 && user.Balance <= 0)
                            break;

                        decimal amountFromRequest = 0;
                        decimal amountFromBalance = 0;

                        // First use request amount
                        if (remainingRequestAmount > 0)
                        {
                            amountFromRequest = Math.Min(remainingRequestAmount, remainingFee);
                            remainingRequestAmount -= amountFromRequest;
                            remainingFee -= amountFromRequest;
                            totalPaidFromRequest += amountFromRequest;
                        }

                        // Then use balance
                        if (remainingFee > 0 && user.Balance > 0)
                        {
                            amountFromBalance = Math.Min(user.Balance, remainingFee);
                            user.Balance -= amountFromBalance;
                            remainingFee -= amountFromBalance;
                            totalPaidFromBalance += amountFromBalance;
                        }

                        decimal totalPaid = amountFromRequest + amountFromBalance;

                        // Skip if nothing paid
                        if (totalPaid <= 0)
                            continue;

                        // Update fee
                        entranceFee.PaidAmount = (entranceFee.PaidAmount ?? 0) + totalPaid;
                        entranceFee.IsPaid = entranceFee.PaidAmount >= entranceFee.Amount;

                        _logger.LogDebug("Processed entrance fee: TripNumber={TripNumber}, Paid={TotalPaid} (Request:{FromRequest}, Balance:{FromBalance}), IsPaid={IsPaid}",
                            entranceFee.TripNumber, totalPaid, amountFromRequest, amountFromBalance, entranceFee.IsPaid);

                        // Payment record
                        var payment = new Payment
                        {
                            Id = Guid.NewGuid(),
                            Amount = totalPaid,
                            PaidAt = entranceFee.TripDate.HasValue
                                ? DateOnly.FromDateTime(entranceFee.TripDate.Value)
                                : request.PaidAt,
                            Car = car,
                            User = user,
                            PaymentType = PaymentType.EntranceFees,
                            TripNumber = entranceFee.TripNumber
                        };

                        await _paymentRepository.AddAsync(payment);
                        feesProcessed++;
                    }

                    // Remaining request amount becomes balance
                    if (remainingRequestAmount > 0)
                    {
                        user.Balance += remainingRequestAmount;
                        _logger.LogInformation("Remaining request amount {RemainingAmount} added to user balance. New balance: {Balance}",
                            remainingRequestAmount, user.Balance);
                    }

                    await _unitOfWork.SaveChangesAsync();

                    var message = $"Entrance fee payment created successfully. Processed {feesProcessed} fees. " +
                                 $"Paid: {totalPaidFromRequest:F2} from payment, {totalPaidFromBalance:F2} from balance.";

                    _logger.LogInformation("Entrance fee payment completed: UserId={UserId}, ProcessedFees={ProcessedCount}, PaidFromRequest={FromRequest}, PaidFromBalance={FromBalance}, RemainingBalance={Balance}",
                        user.Id, feesProcessed, totalPaidFromRequest, totalPaidFromBalance, user.Balance);

                    return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>
                        .Ok(new CreateMonthlyRentalPaymentResponseDtos(Guid.NewGuid()), message);
                }
                else
                {
                    _logger.LogError("Invalid payment type: {PaymentType}", request.PaymentType);
                    return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail($"Invalid payment type: {request.PaymentType}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating payment for UserId: {UserId}, PaymentType: {PaymentType}",
                    request.UserId, request.PaymentType);
                return ApiResponse<CreateMonthlyRentalPaymentResponseDtos>.Fail("An unexpected error occurred while processing the payment. Please try again later.");
            }
        }

        public async Task<CarSummaryDto> GetMonthlySummaryAsync(string carPlate)
        {
            if (string.IsNullOrWhiteSpace(carPlate))
            {
                _logger.LogError("GetMonthlySummaryAsync failed: CarPlate is null or empty");
                throw new ArgumentNullException(nameof(carPlate), "Car plate cannot be null or empty.");
            }

            _logger.LogInformation("Getting monthly summary for CarPlate: {CarPlate}", carPlate);

            try
            {
                // ── 1. Car ────────────────────────────────────────────────────────────
                var car = await _carRepository
                    .GetAll()
                    .Include(c => c.Client)
                    .FirstOrDefaultAsync(c => c.CarPlate == carPlate);

                if (car == null)
                {
                    _logger.LogError("Car not found: {CarPlate}", carPlate);
                    throw new KeyNotFoundException($"Car '{carPlate}' not found.");
                }

                var client = car.Client;
                _logger.LogDebug("Car found: {CarPlate}, Client: {ClientName}, ClientId: {ClientId}",
                    carPlate, client.Name, client.Id);

                var paymentDay = client.DateOfPayment.HasValue ? client.DateOfPayment.Value.Day : 1;
                _logger.LogDebug("Payment day determined: {PaymentDay}", paymentDay);

                // ── 2. Load rental schedule from JSON ─────────────────────────────────
                var schedule = string.IsNullOrEmpty(client.PaymentScheduleJson)
                    ? new List<PaymentScheduleItem>()
                    : JsonSerializer.Deserialize<List<PaymentScheduleItem>>(
                          client.PaymentScheduleJson,
                          new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                var scheduleByMonth = schedule.ToDictionary(s => (s.Year, s.Month));
                _logger.LogDebug("Loaded {ScheduleCount} schedule items for client", schedule.Count);

                // ── 3. Fines ───────────────────────────────────────────────────────────
                var fines = await _fineRepository
                    .GetAll()
                    .Where(f => f.Car.CarPlate == carPlate && f.ViolationDate.HasValue)
                    .ToListAsync();

                var finesByMonth = fines
                    .GroupBy(f => (f.ViolationDate!.Value.Year, f.ViolationDate.Value.Month))
                    .ToDictionary(
                        g => g.Key,
                        g => (Total: g.Sum(f => f.Amount), Count: g.Count()));

                _logger.LogDebug("Loaded {FineCount} fines for car", fines.Count);

                // ── 4. Entrance fees ───────────────────────────────────────────────────
                var fees = await _entrancefeeRepository
                    .GetAll()
                    .Where(e => e.Car.CarPlate == carPlate && e.TripDate.HasValue)
                    .ToListAsync();

                var feesByMonth = fees
                    .GroupBy(e => (e.TripDate!.Value.Year, e.TripDate.Value.Month))
                    .ToDictionary(
                        g => g.Key,
                        g => (Total: g.Sum(e => e.Amount), Count: g.Count()));

                _logger.LogDebug("Loaded {FeeCount} entrance fees for car", fees.Count);

                // ── 5. Fines PAID ──────────────────────────────────────────────────────
                var violationDateByNumber = fines
                    .Where(f => f.ViolationNumber != null)
                    .ToDictionary(f => f.ViolationNumber!, f => f.ViolationDate!.Value);

                var payments = await _paymentRepository
                    .GetAll()
                    .Where(p => p.Car.CarPlate == carPlate)
                    .ToListAsync();

                var finesPaidByViolationMonth = payments
                    .Where(p => p.PaymentType == PaymentType.Fines
                                && p.ViolationNumber != null
                                && violationDateByNumber.ContainsKey(p.ViolationNumber!))
                    .GroupBy(p => (
                        violationDateByNumber[p.ViolationNumber!].Year,
                        violationDateByNumber[p.ViolationNumber!].Month))
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

                // ── 6. Entrance fees PAID ──────────────────────────────────────────────
                var tripDateByNumber = fees
                    .Where(e => e.TripNumber != null)
                    .ToDictionary(e => e.TripNumber!, e => e.TripDate!.Value);

                var feesPaidByTripMonth = payments
                    .Where(p => p.PaymentType == PaymentType.EntranceFees
                                && p.TripNumber != null
                                && tripDateByNumber.ContainsKey(p.TripNumber!))
                    .GroupBy(p => (
                        tripDateByNumber[p.TripNumber!].Year,
                        tripDateByNumber[p.TripNumber!].Month))
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

                // ── 7. Build rows ─────────────────────────────────────────────────────
                var startDate = new DateOnly(client.JoinDate.Year, client.JoinDate.Month, 1);

                // Check earliest fine date
                var earliestFineDate = fines
                    .Where(f => f.ViolationDate.HasValue)
                    .Select(f => f.ViolationDate!.Value)
                    .OrderBy(d => d)
                    .FirstOrDefault();

                // Check earliest entrance fee date
                var earliestFeeDate = fees
                    .Where(e => e.TripDate.HasValue)
                    .Select(e => e.TripDate!.Value)
                    .OrderBy(d => d)
                    .FirstOrDefault();

                // Compare and use the earliest month
                if (earliestFineDate != default)
                {
                    var fineStart = new DateOnly(earliestFineDate.Year, earliestFineDate.Month, 1);
                    if (fineStart < startDate)
                        startDate = fineStart;
                }

                if (earliestFeeDate != default)
                {
                    var feeStart = new DateOnly(earliestFeeDate.Year, earliestFeeDate.Month, 1);
                    if (feeStart < startDate)
                        startDate = feeStart;
                }

                var endDate = new DateOnly(client.ContractExpiry.Year, client.ContractExpiry.Month, 1);
                _logger.LogDebug("Summary date range: {StartDate} to {EndDate}", startDate, endDate);

                var rows = new List<CarMonthlyRowDto>();
                var cursor = startDate;
                var monthsProcessed = 0;

                while (cursor <= endDate)
                {
                    var year = cursor.Year;
                    var month = cursor.Month;
                    var key = (year, month);

                    scheduleByMonth.TryGetValue(key, out var sched);
                    finesByMonth.TryGetValue(key, out var fineData);
                    feesByMonth.TryGetValue(key, out var feeData);
                    finesPaidByViolationMonth.TryGetValue(key, out var finesPaid);
                    feesPaidByTripMonth.TryGetValue(key, out var feesPaid);

                    var rentalPrice = sched?.Amount ?? 0m;
                    var rentalPaid = sched?.RentalPaid ?? 0m;
                    var totalPaid = rentalPaid + finesPaid + feesPaid;

                    // Latest payment date across all payment types this month
                    var latestDate = payments
                        .Where(p => p.PaidAt.Year == year && p.PaidAt.Month == month)
                        .Select(p => (DateTime?)p.PaidAt.ToDateTime(TimeOnly.MinValue))
                        .DefaultIfEmpty(sched?.PaidAt)
                        .Max();

                    rows.Add(new CarMonthlyRowDto(
                        Year: year,
                        Month: month,
                        PaymentDate: latestDate?.ToString("yyyy-MM-dd"),
                        RentalPrice: rentalPrice,
                        RentalPaid: rentalPaid,
                        FinesPaid: finesPaid,
                        EntranceFeesPaid: feesPaid,
                        AmountPaid: totalPaid,
                        TotalFines: fineData.Total,
                        FinesCount: fineData.Count,
                        TotalEntranceFees: feeData.Total,
                        EntranceFeesCount: feeData.Count
                    ));

                    cursor = cursor.AddMonths(1);
                    monthsProcessed++;
                }

                _logger.LogInformation("Monthly summary generated for CarPlate: {CarPlate}, Months: {MonthsCount}", carPlate, monthsProcessed);

                return new CarSummaryDto(
                    ClientId: client.Id,
                    CarPlate: car.CarPlate,
                    Brand: car.Brand,
                    Model: car.Model,
                    CarYear: car.Year,
                    Rows: rows,
                    JoinDate: client.JoinDate,
                    ContractExpiry: client.ContractExpiry,
                    UserName: client.Name,
                    paymentDay,
                    Balance: client?.Balance,
                    DownPayment:client?.DownPayment
                );
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly summary for CarPlate: {CarPlate}", carPlate);
                throw new InvalidOperationException("An error occurred while generating the monthly summary. Please try again later.", ex);
            }
        }

        public async Task<string?> AddRentalPaymentAsync(Guid clientId, AddRentalPaymentDto dto)
        {
            if (dto == null)
            {
                _logger.LogError("AddRentalPaymentAsync failed: DTO is null");
                throw new ArgumentNullException(nameof(dto), "Payment DTO cannot be null.");
            }

            if (dto.Amount <= 0)
            {
                _logger.LogError("AddRentalPaymentAsync failed: Invalid amount {Amount} for ClientId: {ClientId}", dto.Amount, clientId);
                throw new InvalidOperationException("Payment amount must be greater than zero.");
            }

            _logger.LogInformation("Adding rental payment for ClientId: {ClientId}, Month: {Month}/{Year}, Amount: {Amount}",
                clientId, dto.Month, dto.Year, dto.Amount);

            try
            {
                var client = await _userRepository.GetAll()
                    .Include(c => c.Cars)
                    .FirstOrDefaultAsync(x => x.Id == clientId);

                if (client == null)
                {
                    _logger.LogError("Client not found: {ClientId}", clientId);
                    throw new KeyNotFoundException($"Client with ID '{clientId}' not found.");
                }

                _logger.LogDebug("Client found: {ClientId}, Name: {ClientName}", client.Id, client.Name);

                var schedule = string.IsNullOrEmpty(client.PaymentScheduleJson)
                    ? new List<PaymentScheduleItem>()
                    : JsonSerializer.Deserialize<List<PaymentScheduleItem>>(
                        client.PaymentScheduleJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                var entry = schedule.FirstOrDefault(p => p.Month == dto.Month && p.Year == dto.Year);
                if (entry == null)
                {
                    _logger.LogError("No rental schedule entry found for ClientId: {ClientId}, Month: {Month}/{Year}",
                        clientId, dto.Month, dto.Year);
                    throw new KeyNotFoundException($"No rental schedule entry for {dto.Month}/{dto.Year}.");
                }

                _logger.LogDebug("Schedule entry found: Month={Month}/{Year}, Amount={Amount}, Paid={RentalPaid}",
                    entry.Month, entry.Year, entry.Amount, entry.RentalPaid);

                decimal remaining = entry.Amount - entry.RentalPaid;
                string? balanceMessage = null;
                decimal amountToApply = dto.Amount;

                if (dto.Amount > remaining)
                {
                    decimal excess = dto.Amount - remaining;
                    amountToApply = remaining;

                    client.Balance += excess;
                    balanceMessage = $"Monthly rental fully paid. Excess amount of {excess:F2} EGP has been added to your balance. New balance: {client.Balance:F2} EGP.";
                    _logger.LogInformation("Excess payment for rental: {Excess} added to client balance. New balance: {Balance}", excess, client.Balance);
                }

                entry.RentalPaid += amountToApply;
                entry.IsPaid = entry.RentalPaid >= entry.Amount;
                entry.PaidAt = DateTime.UtcNow;

                client.PaymentScheduleJson = JsonSerializer.Serialize(schedule);

                var car = client.Cars.FirstOrDefault();
                if (car == null)
                {
                    _logger.LogError("No car linked to client: {ClientId}", clientId);
                    throw new InvalidOperationException("No car linked to this client.");
                }

                await _paymentRepository.AddAsync(new Payment
                {
                    Id = Guid.NewGuid(),
                    Amount = amountToApply,
                    PaidAt = DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentType = PaymentType.MonthlyRental,
                    Car = car,
                    User = client
                });

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Rental payment added successfully: ClientId={ClientId}, Amount={Amount}, Month={Month}/{Year}",
                    clientId, amountToApply, dto.Month, dto.Year);

                return balanceMessage;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding rental payment for ClientId: {ClientId}, Month: {Month}/{Year}",
                    clientId, dto.Month, dto.Year);
                throw new InvalidOperationException("An error occurred while adding the rental payment. Please try again later.", ex);
            }
        }

        public async Task UpdateAsync(Guid id, UpdateMonthlyRentalPaymentRequestDto request)
        {
            if (request == null)
            {
                _logger.LogError("UpdateAsync failed: Request is null for PaymentId: {PaymentId}", id);
                throw new ArgumentNullException(nameof(request), "Update request cannot be null.");
            }

            _logger.LogInformation("Updating payment: PaymentId={PaymentId}, NewAmount={Amount}, NewPaidAt={PaidAt}",
                id, request.Amount, request.PaidAt);

            try
            {
                var payment = await _paymentRepository.GetByIdAsync(id);
                if (payment == null)
                {
                    _logger.LogError("Payment not found for update: {PaymentId}", id);
                    throw new KeyNotFoundException($"Payment with ID '{id}' not found.");
                }

                var oldAmount = payment.Amount;
                payment.Amount = request.Amount;
                payment.PaidAt = request.PaidAt;

                await _paymentRepository.UpdateAsync(payment);
                await _paymentRepository.SaveChanges();

                _logger.LogInformation("Payment updated successfully: PaymentId={PaymentId}, OldAmount={OldAmount}, NewAmount={NewAmount}",
                    id, oldAmount, request.Amount);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment: {PaymentId}", id);
                throw new InvalidOperationException("An error occurred while updating the payment. Please try again later.", ex);
            }
        }

        public async Task<PagedResult<MonthlyRentalPaymentDto>> GetAllAsync(
            int page, int pageSize,
            string? search = null, string? searchBy = null,
            string? paymentType = null)
        {
            _logger.LogInformation("Getting all payments: Page={Page}, PageSize={PageSize}, Search={Search}, SearchBy={SearchBy}, PaymentType={PaymentType}",
                page, pageSize, search ?? "null", searchBy ?? "null", paymentType ?? "null");

            try
            {
                IQueryable<Payment> query = _paymentRepository.GetAll();

                // ── Payment type filter ───────────────────────────────────────────────
                if (!string.IsNullOrWhiteSpace(paymentType) && Enum.TryParse<PaymentType>(paymentType, ignoreCase: true, out var parsedType))
                {
                    query = query.Where(p => p.PaymentType == parsedType);
                    _logger.LogDebug("Filtering by payment type: {PaymentType}", parsedType);
                }

                // ── Search filter ─────────────────────────────────────────────────────
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToLower();
                    query = searchBy switch
                    {
                        "username" => query.Where(p => p.User.Name.ToLower().Contains(term)),
                        "carplate" => query.Where(p => p.Car.CarPlate.ToLower().Contains(term)),
                        _ => query
                    };
                    _logger.LogDebug("Searching by {SearchBy} with term: {SearchTerm}", searchBy ?? "default", term);
                }

                var totalCount = await query.CountAsync();
                _logger.LogDebug("Total payments found: {TotalCount}", totalCount);

                var items = await query
                    .OrderByDescending(p => p.PaidAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new MonthlyRentalPaymentDto(
                        p.Id,
                        p.Amount,
                        p.PaidAt,
                        p.Car.CarPlate,
                        p.User.Id,
                        p.User.Name,
                        p.PaymentType
                    ))
                    .ToListAsync();

                _logger.LogInformation("Retrieved {ItemCount} payments for page {Page}", items.Count, page);

                return new PagedResult<MonthlyRentalPaymentDto>(items, totalCount, page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated payments");
                throw new InvalidOperationException("An error occurred while retrieving payments. Please try again later.", ex);
            }
        }

        public async Task<MonthlyRentalPaymentDto> GetByIdAsync(Guid id)
        {
            _logger.LogInformation("Getting payment by ID: {PaymentId}", id);

            try
            {
                var dto = await _paymentRepository
                    .GetAll()
                    .Where(x => x.Id == id)
                    .Select(x => new MonthlyRentalPaymentDto(
                        x.Id,
                        x.Amount,
                        x.PaidAt,
                        x.Car.CarPlate,
                        x.User.Id,
                        x.User.Name,
                        x.PaymentType
                    ))
                    .FirstOrDefaultAsync();

                if (dto == null)
                {
                    _logger.LogError("Payment not found: {PaymentId}", id);
                    throw new KeyNotFoundException($"Payment with ID '{id}' not found.");
                }

                _logger.LogInformation("Payment retrieved: {PaymentId}, Type: {PaymentType}, Amount: {Amount}",
                    dto.Id, dto.PaymentType, dto.Amount);

                return dto;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment by ID: {PaymentId}", id);
                throw new InvalidOperationException("An error occurred while retrieving the payment. Please try again later.", ex);
            }
        }

        public async Task<SystemFinancialSummaryDto> GetSystemMonthlySummaryAsync()
        {
            _logger.LogInformation("Generating system financial summary");

            try
            {
                var usersCount = await _userRepository.GetAll().CountAsync();
                _logger.LogDebug("Total users count: {UsersCount}", usersCount);

                var payments = await _paymentRepository.GetAll().ToListAsync();
                var totalRevenue = payments.Sum(p => p.Amount);
                _logger.LogDebug("Total revenue: {TotalRevenue:C}", totalRevenue);

                var fines = await _fineRepository
                    .GetAll()
                    .Where(f => f.ViolationDate.HasValue && !f.IsPaid)
                    .ToListAsync();

                var fees = await _entrancefeeRepository
                    .GetAll()
                    .Where(e => e.TripDate.HasValue && !e.IsPaid)
                    .ToListAsync();

                var totalFines = fines.Sum(f => f.Amount);
                var totalFees = fees.Sum(e => e.Amount);
                var totalDebt = totalFines + totalFees;
                var netBalance = totalRevenue - totalDebt;

                _logger.LogDebug("Fines: Total={TotalFines:C}, Count={FinesCount}", totalFines, fines.Count);
                _logger.LogDebug("Entrance Fees: Total={TotalFees:C}, Count={FeesCount}", totalFees, fees.Count);
                _logger.LogDebug("Total Debt: {TotalDebt:C}, Net Balance: {NetBalance:C}", totalDebt, netBalance);

                // ── Unpaid rentals: sum (Amount - RentalPaid) across all client schedules ──
                var clients = await _userRepository.GetAll().ToListAsync();
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var now = DateTime.UtcNow;

                var totalUnpaidRentals = clients
                    .Where(c => !string.IsNullOrEmpty(c.PaymentScheduleJson))
                    .SelectMany(c => {
                        var payDay = c.DateOfPayment.HasValue ? c.DateOfPayment.Value.Day : 1;
                        return (JsonSerializer.Deserialize<List<PaymentScheduleItem>>(
                                    c.PaymentScheduleJson!, jsonOptions)
                                ?? new List<PaymentScheduleItem>())
                            .Where(s => new DateTime(s.Year, s.Month, Math.Min(payDay, DateTime.DaysInMonth(s.Year, s.Month))) <= now)
                            .Select(s => (s.Amount, s.RentalPaid));
                    })
                    .Sum(s => Math.Max(0m, s.Amount - s.RentalPaid));

                _logger.LogInformation("Total unpaid rentals: {TotalUnpaidRentals:C}", totalUnpaidRentals);

                var summary = new SystemFinancialSummaryDto(
                    TotalRevenue: totalRevenue,
                    TotalDebt: totalDebt,
                    NetBalance: netBalance,
                    TotalFines: totalFines,
                    TotalEntranceFees: totalFees,
                    FinesCount: fines.Count,
                    EntranceFeesCount: fees.Count,
                    UsersCount: usersCount,
                    TotalUnpaidRentals: totalUnpaidRentals
                );

                _logger.LogInformation("System financial summary generated successfully");
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system financial summary");
                throw new InvalidOperationException("An error occurred while generating the financial summary. Please try again later.", ex);
            }
        }
    }
}