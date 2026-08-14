import { toNodeListener } from 'h3';
import { createServer } from 'http';
import handler from './.output/server/index.mjs';
const server = createServer(toNodeListener(handler));
server.listen(4173, () => console.log('listening on 4173'));
