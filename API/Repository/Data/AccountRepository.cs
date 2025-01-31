﻿using API.Context;
using API.Models;
using API.ViewModel;
using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.Repository.Data
{
    //This class to implement GeneralRepository in Account
    public class AccountRepository : GeneralRepository<MyContext, Account, string>
    {
        private readonly MyContext context;

        public AccountRepository(MyContext myContext) : base(myContext)
        {
            context = myContext;
        }

        //This method used to change password
        //account used to get a row from entities by Email
        //check all condition are change same with account
        //send potential value if some condtion not passed
        //then update password and IsUsed = true if all condition passed
        public int ChangePassord(ChangePasswordVM change)
        {
            var account = context.Employees
                .Join(context.Accounts, a => a.NIK, e => e.NIK,
                (e, a) => new
                {
                    NIK = a.NIK,
                    Email = e.Email,
                    Password = a.Password,
                    OTP = a.OTP,
                    ExpiredToken = a.ExpiredToken,
                    IsUsed = a.IsUsed
                }).SingleOrDefault(e => e.Email == change.Email);

            if (account != null)
            {
                if (change.OTP == account.OTP)
                {
                    if (account.IsUsed == false)
                    {
                        if (account.ExpiredToken > DateTime.Now)
                        {
                            if (change.NewPassword == change.ConfirmPassword)
                            {
                                base.Update(new Account
                                {
                                    NIK = account.NIK,
                                    Password = HashPassword(change.NewPassword),
                                    OTP = account.OTP,
                                    ExpiredToken = account.ExpiredToken,
                                    IsUsed = true
                                });
                                return 5;
                            }
                            return 4;
                        }
                        return 3;
                    }
                    return 2;
                }
                return 1;
            }
            return 0;
        }

        //This method used to send OTP to employee email
        //account used to get a row from entities by parameter email
        //if account is not empty condition passed
        //acc to generate OTP, set OTP can used for 5min then update Account
        //send OTP to employee email
        public int ForgotPassword(String email)
        {
            var account = context.Employees.Join
                (context.Accounts, a => a.NIK, e => e.NIK,
                (e, a) => new
                {
                    NIK = a.NIK,
                    Email = e.Email,
                    Password = a.Password
                }).SingleOrDefault(e => e.Email == email);

            if (account != null)
            {
                Account acc = new Account
                {
                    NIK = account.NIK,
                    Password = account.Password,
                    OTP = new Random().Next(100000, 999999),
                    ExpiredToken = DateTime.Now.AddMinutes(5),
                    IsUsed = false
                };
                Update(acc);

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Forgot Password EF Core", "kaibee3333@gmail.com"));
                message.To.Add(MailboxAddress.Parse(email));
                message.Subject = $"Change Password OTP ({acc.OTP})";
                message.Body = new TextPart("Plain") { Text = $"Kode OTP : {acc.OTP}" };

                SmtpClient smtp = new SmtpClient();
                smtp.Connect("smtp.gmail.com", 465, true);
                smtp.Authenticate("kaibee3333@gmail.com", "Kaibe333");
                smtp.Send(message);
                smtp.Disconnect(true);

                return 1;
            }
            return 0;
        }

        //This method used to Login
        //check used to get a row from entities by parameter login
        //if check is not empty condition passed
        //call ValidatePassword method to check is parameter login equals check
        public int Login(LoginVM login)
        {
            var checkAccount = context.Employees
                .Join(context.Accounts, e => e.NIK, a => a.NIK,
                (e, a) => new
                {
                    Email = e.Email,
                    Password = a.Password
                }).SingleOrDefault(e => e.Email == login.Email);

            if (checkAccount != null)
            {
                return ValidatePassword(login.Password, checkAccount.Password)
                    ? 2
                    : 1;
            }
            return 0;
        }

        //This method used to register
        //generate NIK by Year and Count rows in employee entity
        //call CheckEmailPhone by parameter email and phone from register
        //if condition passed add all to entities
        public int Register(RegisterVM register)
        {
            var Year = DateTime.Now.Year;
            var empCount = context.Employees.OrderByDescending(e => e.NIK).FirstOrDefault();

            if (empCount == null)
            {
                register.NIK = Year + "00" + 1;
            }
            else
            {
                register.NIK = Convert.ToString(Convert.ToInt32(empCount.NIK) + 1);
            }
            
            if (CheckEmailPhone(register.Email, register.Phone))
            {
                Employee emp = new Employee
                {
                    NIK = register.NIK,
                    FirstName = register.FirstName,
                    LastName = register.LastName,
                    BirthDate = register.BirthDate,
                    Gender = register.Gender,
                    Phone = register.Phone,
                    Salary = register.Salary,
                    Email = register.Email
                };
                context.Employees.Add(emp);

                context.Accounts.Add(new Account
                {
                    NIK = emp.NIK,
                    Password = HashPassword(register.Password)
                });

                Education edu = new Education
                {
                    GPA = register.GPA,
                    Degree = register.Degree,
                    UniversityId = register.UniversityId
                };
                context.Educations.Add(edu);
                context.SaveChanges();

                context.AccountRoles.Add(new AccountRole
                {
                    AccountNIK = emp.NIK,
                    RoleId = 1
                });

                context.Profilings.Add(new Profiling
                {
                    NIK = emp.NIK,
                    EducationId = edu.Id
                });
                context.SaveChanges();

                return 1;
            }
            return 0;
        }

        //This method used to get all role that contains email
        public List<string> UserRole(string email)
        {
            var checkNIK = context.Employees.SingleOrDefault(e => e.Email == email).NIK;

            var checkRole = context.AccountRoles
                .Where(ar => ar.AccountNIK == checkNIK)
                .Join(context.Roles, ar => ar.RoleId, r => r.Id, (ar, r) => r.Name)
                .ToList();

            return checkRole;
        }

        //This method used to check are email and phone is have been used or not
        private bool CheckEmailPhone(string email, string phone)
        {
            return context.Employees.Where(s => s.Email == email || s.Phone == phone).SingleOrDefault() == null;
        }

        //This method used for plain text to chiper text using BCrypt with Salt 12
        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        //This method used to validate password using BCrypt
        private static bool ValidatePassword(string password, string correctHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, correctHash);
        }
    }
}
