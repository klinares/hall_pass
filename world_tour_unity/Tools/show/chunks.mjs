const { chunks } = await import(new URL('../../../world_tour/js/show.js', import.meta.url).href);
for (const v of [{w:'beautiful',s:'beau-ti-ful'},{w:'summer',s:'sum-mer'},{w:'excitement',s:'ex-cit-e-ment'},{w:'cat'},{w:'cat',s:''},{w:'jump',s:'jump'},{w:'go'}]) console.log(JSON.stringify(chunks(v)));
process.exit(0);
