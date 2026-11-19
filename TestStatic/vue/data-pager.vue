
<!-- Vue to ES Module test component -->

<template id="data-pager">
	<div class="pager">
		<div class="pages" v-show="showPages">
			<template v-if="pageFrom>1"><button @click.stop="_emitPage(1)">1</button> …</template>
			<template v-for="p in (pageTo-pageFrom+1)">
				<button v-if="(pageFrom+p-1)===page" class="active" disabled>{{ pageFrom+p-1 }}</button>
				<button v-else @click.stop="_emitPage(pageFrom+p-1)">{{ pageFrom+p-1 }}</button>
			</template>
			<template v-if="pageTo<maxPage">… <button @click.stop="_emitPage(maxPage)">{{ maxPage }}</button></template>
			<span><slot></slot></span>
		</div>
	</div>
</template>

<script>
	define(['lib/vue'], function(Vue)
	{
		const SHOWPAGEDELTA		= 2;
		const pager				= Vue.extend({
			name				: 'data-pager',
			template 			: '#data-pager',
			props				: {
				skip			: Number,
				take			: Number,
				count			: Number,
			},
			computed			: {
				safeSkip		: function() { return this.skip>0 ? this.skip : 0; },
				safeTake		: function() { return this.take>0 ? Math.ceil(this.take) : 1; },
				page			: function() { return Math.floor(this.safeSkip/this.safeTake)+1; },
				maxPage			: function() { return this.count>0 ? Math.ceil(this.count/this.safeTake) : 0; },
				showPages		: function() { return this.maxPage>1; },
				pageFrom		: function() { const v = Math.max(this.page-SHOWPAGEDELTA, 1);            return (v<SHOWPAGEDELTA+2) ? 1 : v; },
				pageTo			: function() { const v = Math.min(this.page+SHOWPAGEDELTA, this.maxPage); return (v+1>=this.maxPage) ? this.maxPage : v; },
			},
			methods				: {
				_emitPage		: function(p)
				{
					this.$emit('page', { page: p, skip: p===1 ? 0 : ((p-1)*this.safeTake), take: this.safeTake });
				}
			}
		});

		return pager;
	});
</script>

<style>
    .pager {
		display: block;
    }
</style>