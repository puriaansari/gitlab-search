// Fixtures model the endpoints consumed by the original GitLab.re and GitLab API docs.
const http = require('http');
const attempts = new Map();
const requests = [];
function count(key) { const n = (attempts.get(key) || 0) + 1; attempts.set(key, n); return n; }
http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  const path = url.pathname;
  res.setHeader('Content-Type', 'application/json');
  const record = {path: req.url, at: Date.now()};
  function send(code, data) { record.status = code; res.statusCode = code; res.end(JSON.stringify(data)); }
  if (req.headers['private-token'] !== 'fixture-token') return send(401, { message: '401 Unauthorized' });
  if (path === '/__test/requests') return send(200, requests);
  requests.push(record);
  if (path === '/api/v4/groups') {
    if (!url.searchParams.has('page')) {
      res.setHeader('X-Next-Page', '2'); return send(200, [{id:1,name:'one'}]);
    }
    return send(200, [{id:2,name:'two'}]);
  }
  if (/^\/api\/v4\/groups\/(1|2|team%2Fsub|card)\/projects$/i.test(path)) {
    if (url.searchParams.get('include_subgroups') !== 'true') return send(200, []);
    const archived = url.searchParams.get('archived');
    const items = [
      {id:10,name:'sample',web_url:'https://gitlab.example/team/sample',archived:false},
      {id:11,name:'old',web_url:'https://gitlab.example/team/old',archived:true}
    ];
    return send(200,items.filter(p => archived === null || String(p.archived) === archived));
  }
  if (path === '/api/v4/projects/11/search') return send(200,[]);
  if (path === '/api/v4/projects/10/search') {
    const search = url.searchParams.get('search');
    if (search === 'error') return send(403,{message:'Forbidden'});
    if (search === 'unsafe') {
      res.setHeader('Link','<https://evil.example/api/v4/groups>; rel="next"'); return send(200,[]);
    }
    if (search && search.startsWith('retry-')) {
      const key = search + ':' + (url.searchParams.get('page') || '1');
      const attempt = count(key);
      const shouldFail = search === 'retry-limit' || search === 'retry-no-header' || search === 'retry-cancel'
        || (search === 'retry-page' ? url.searchParams.get('page') === '2' && attempt === 1 : attempt === 1);
      if (shouldFail) {
        if (search === 'retry-date') res.setHeader('Retry-After', new Date(Date.now() + 2000).toUTCString());
        else if (search === 'retry-cancel') res.setHeader('Retry-After', '600');
        else if (search !== 'retry-no-header') res.setHeader('Retry-After', '1');
        return send(429, {message:'Too Many Requests'});
      }
    }
    if (url.searchParams.get('scope') !== 'blobs') return send(400,{message:'Wrong scope'});
    const page = url.searchParams.get('page');
    if (!page) res.setHeader('Link',`<http://${req.headers.host}/gitlab/api/v4/projects/10/search?scope=blobs&per_page=100&search=${encodeURIComponent(search)}&page=2>; rel="next"`);
    return send(200,[{data:search,filename:page ? 'src/Second.cs' : 'src/First file.cs',ref:'main',startline:page ? 8 : 3}]);
  }
  send(501,{message:'Endpoint not modeled'});
}).listen(process.env.PORT || 4500);
