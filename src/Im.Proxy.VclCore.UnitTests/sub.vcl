backend F_Legacy {
	.host="localhost";
	.port="8881";
}

backend F_Identity {
	.host="localhost";
	.port="8882";
}

sub vcl_recv {
  if (req.http.FastlyFF) {
    set req.max_stale_while_revalidate = 0s;
  }

  set req.http.X-Backend = req.backend;

  if (req.request != "HEAD" && req.request != "GET" && req.request != "FASTLYPURGE") {
    return(pass);
  }

  return(lookup);
}
