// Variables for the `.http` contract suite.
//
// This server's management surface is anonymous — it carries no secret and answers questions about
// the running process — so unlike the vault's suite there are no tokens to mint here. The one thing
// worth configuring is where the server is, and `http-run.mjs --target` overrides it anyway.

module.exports = {
  environments: {
    local: {
      baseUrl: process.env.MCP_BASE_URL ?? 'http://127.0.0.1:5211',
    },
  },
};
