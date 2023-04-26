﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExamBook.Entities;
using ExamBook.Exceptions;
using ExamBook.Helpers;
using ExamBook.Identity.Models;
using ExamBook.Models;
using ExamBook.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vx.Models;
using Vx.Services;

namespace ExamBook.Services
{
    public class ClassroomSpecialityService
    {
        private readonly DbContext _dbContext;
        private readonly ILogger<ClassroomSpecialityService> _logger;
        private readonly EventService _eventService;
        private readonly PublisherService _publisherService;

        public ClassroomSpecialityService(DbContext dbContext, ILogger<ClassroomSpecialityService> logger, 
            EventService eventService, 
            PublisherService publisherService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _eventService = eventService;
            _publisherService = publisherService;
        }


        public async Task<ActionResultModel<ClassroomSpeciality>> AddSpeciality(Classroom classroom, 
            Speciality speciality, User user)
        {
            Asserts.NotNull(classroom.Space, nameof(classroom.Space));
            Asserts.NotNull(user, nameof(user));
            var classroomSpeciality = await CreateSpecialityAsync(classroom, speciality);
            var publisher = await _publisherService.AddAsync();
            classroomSpeciality.PublisherId = publisher.Id;
            
            await _dbContext.AddAsync(classroomSpeciality);
            await _dbContext.SaveChangesAsync();

            var publisherIds = new List<string>
            {
                publisher.Id, 
                classroom.PublisherId, 
                classroom.Space!.PublisherId,
                speciality.PublisherId,
            };
            const string eventName = "CLASSROOM_SPECIALITY_ADD";
            var @event = await _eventService.EmitAsync(publisherIds, user.ActorId, eventName, classroomSpeciality);
            
            _logger.LogInformation("New classroom speciality");
            return new ActionResultModel<ClassroomSpeciality>(classroomSpeciality, @event);
        }
        
        
        
        public async Task<List<ClassroomSpeciality>> AddClassroomSpecialitiesAsync(
            Classroom classroom,
            List<ulong> specialityIds)
        {
            var classroomSpecialities = await CreateClassroomSpecialitiesAsync(classroom, specialityIds);
            await _dbContext.AddRangeAsync(classroomSpecialities);
            await _dbContext.SaveChangesAsync();
            return classroomSpecialities;
        }
        
        
        public async Task<Event> DeleteSpecialityAsync(ClassroomSpeciality classroomSpeciality, User user)
        {
            Asserts.NotNull(classroomSpeciality, nameof(classroomSpeciality));
            Asserts.NotNull(classroomSpeciality.Classroom, nameof(classroomSpeciality.Classroom));
            Asserts.NotNull(classroomSpeciality.Speciality, nameof(classroomSpeciality.Speciality));
            Asserts.NotNull(classroomSpeciality.Classroom!.Space, nameof(classroomSpeciality.Classroom.Space));
            Asserts.NotNull(user, nameof(user));

            classroomSpeciality.DeletedAt = DateTime.UtcNow;
            _dbContext.Update(classroomSpeciality);
            await _dbContext.SaveChangesAsync();
            
            var publisherIds = new List<string>
            {
                classroomSpeciality.PublisherId, 
                classroomSpeciality.Speciality!.PublisherId,
                classroomSpeciality.Classroom.PublisherId, 
                classroomSpeciality.Classroom.Space!.PublisherId
            };
            const string eventName = "CLASSROOM_SPECIALITY_DELETE";
            return await _eventService.EmitAsync(publisherIds, user.ActorId, eventName, classroomSpeciality);
        }
        
        
        public async Task<List<ClassroomSpeciality>> CreateClassroomSpecialitiesAsync(Classroom classroom,
            List<ulong> specialityIds)
        {
            Asserts.NotNull(classroom, nameof(classroom));
            Asserts.NotNull(specialityIds, nameof(specialityIds));

            var specialities = await _dbContext.Set<Speciality>()
                .Where(s => specialityIds.Contains(s.Id))
                .ToListAsync();

            var classroomSpecialities = new List<ClassroomSpeciality>();

            foreach (var speciality in specialities)
            {
                var classroomSpeciality = await CreateSpecialityAsync(classroom, speciality);
                classroomSpecialities.Add(classroomSpeciality);
            }

            return classroomSpecialities;
        }

        public async Task<ClassroomSpeciality> CreateSpecialityAsync(Classroom classroom, Speciality speciality)
        {
            Asserts.NotNull(classroom, nameof(classroom));
            Asserts.NotNull(speciality, nameof(speciality));
            Asserts.NotNull(classroom.Space, nameof(classroom.Space));

            if (!classroom.Space!.Equals(speciality.Space))
            {
                throw new IncompatibleEntityException(classroom, speciality);
            }

            if (await HasSpecialityAsync(classroom, speciality))
            {
                SpaceHelper.ThrowDuplicateClassroomSpeciality();
            }

            ClassroomSpeciality classroomSpeciality = new()
            {
                Classroom = classroom,
                Speciality = speciality
            };
            
            return classroomSpeciality;
        }

        public async Task<bool> HasSpecialityAsync(Classroom classroom, Speciality speciality)
        {
            Asserts.NotNull(classroom, nameof(classroom));
            Asserts.NotNull(speciality, nameof(speciality));
            return await _dbContext.Set<ClassroomSpeciality>()
                .Where(cs => classroom.Id == cs.ClassroomId && speciality.Id == cs.SpecialityId && cs.DeletedAt == null)
                .AnyAsync();
        }
        
    }
}