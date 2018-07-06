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
    set req.maxstalewhilerevalidate = 0s;
  }

  set req.http.XBackend = req.backend;

  if (req.request != "HEAD" && req.request != "GET" && req.request != "FASTLYPURGE") {
    return(pass);
  }

  return(lookup);
}
