﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Blogging.Blogs;
using Volo.Blogging.Blogs.Dtos;

namespace Volo.Blogging
{
    [RemoteService]
    [Area("blogging")]
    [Controller]
    [ControllerName("Blogs")]
    [Route("api/blogging/blogs")]
    [DisableAuditing]
    public class BlogsController : AbpController, IBlogAppService
    {
        private readonly IBlogAppService _blogAppService;

        public BlogsController(IBlogAppService blogAppService)
        {
            _blogAppService = blogAppService;
        }

        [HttpGet]
        [Route("")]
        public async Task<PagedResultDto<BlogDto>> GetListPagedAsync(PagedAndSortedResultRequestDto input)
        {
            return await _blogAppService.GetListPagedAsync(input);
        }

        [HttpGet]
        [Route("/all")]
        public async Task<ListResultDto<BlogDto>> GetListAsync()
        {
            return await _blogAppService.GetListAsync();
        }

        [HttpGet]
        [Route("{shortName}")]
        public async Task<BlogDto> GetByShortNameAsync(string shortName)
        {
            return await _blogAppService.GetByShortNameAsync(shortName);
        }

        [HttpGet]
        [Route("{id}")]
        public async Task<BlogDto> GetAsync(Guid id)
        {
            return await _blogAppService.GetAsync(id);
        }

        [HttpPost]
        public async Task<BlogDto> Create(CreateBlogDto input)
        {
            return await _blogAppService.Create(input);
        }

        [HttpPut]
        [Route("{id}")]
        public async Task<BlogDto> Update(Guid id, UpdateBlogDto input)
        {
            return await _blogAppService.Update(id, input);
        }

        [HttpDelete]
        public async Task Delete(Guid id)
        {
            await _blogAppService.Delete(id);
        }
    }
}
