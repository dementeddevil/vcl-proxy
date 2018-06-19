sub vcl_recv {
  if (req.http.Fastly-FF) {
    set req.max_stale_while_revalidate = 0s;
  }
  /*
  if( req.http.host == "hitched.co.uk" ) {
    error 900 "Redirect to www";
  }
  */
  
# FASTLY recv
  if (req.http.host ~ "^connect-uat" && (req.url ~ "^/$" || req.url == "")) {
    error 403;
  }
  
  if (req.http.host ~ "^connect-uat" && !req.url ~ "^/live/") {
    set req.backend = F_AWS_connect_api;
  }
    
  if (req.url ~ "^/live/1-0-1-8" || req.url ~ "^/live/1-0-1-7") {
     set req.http.host = "connect-uat.hitched.co.uk";
  }
  
# Bypass header and footer
if (!req.url ~ "^/api/hitched/" && (req.url ~ "^/general" || req.url ~ "^/[aA][pP][iI]" || req.url ~ "^/rimage" || req.url ~ "^/header.aspx" || req.url ~ "^/footer.aspx" || req.http.host ~ "^connect" || req.http.host ~ "^connect-uat" || req.http.host ~ "^connect-old" || req.url ~ "^/google[a-zA-Z0-9]+\.html$")) { 
   return (pass);
}


# Redirect crawlers to old site (Rackspace) 
if (req.http.User-Agent ~ "(?i)(ads|google|bing|msn|yandex|baidu|ro|career|)bot" ||
    req.http.User-Agent ~ "(?i)facebookexternalhit\/1.1 \(+http:\/\/www.facebook.com\/externalhit_uatext.php\)" ||
    req.http.User-Agent ~ "(?i)facebookexternalhit\/1.1" ||
    req.http.User-Agent ~ "(?i)Facebot" ||
    req.http.User-Agent ~ "(?i)(baidu|jike|symantec)spider" ||
    req.http.User-Agent ~ "(?i)scanner" ||
    req.http.User-Agent ~ "(?i)(web)crawler" || 
    req.url ~ "^/webhooks") {
      unset req.http.Cookie;
      set req.backend = F_hitched_legacy;
  
  
# Redirect traffic to new supplier UI if cookie exist 
} else if (req.url ~ "^/enrol") { 
  set req.http.X-Enroll = req.http.Cookie; 
  error 901;
} else if (req.url ~ "^/exit") {
  unset req.http.Cookie;
  error 902;
} else if (!req.http.Cookie ~ "BetaUser=" && req.request == "GET" && !(req.url ~ "/node-resources/" || req.url ~ "/js/" || req.url ~ "/dist/" || req.url ~ "/css/" || req.url ~ "/javascript/" || req.url ~ "/views/" || req.url ~ "/templates/" || req.url ~ "/planner/" || req.url ~ "/images/" || req.url ~ "/newsletter/")) {
    error 903;
}


#FASTLY recv
# Cookie enrollment

if  ((req.http.Cookie ~ "BetaUser=SupplierSearchResults" || req.http.User-Agent ~ "HitchedApp") && (!req.url ~ "^\/wedding-suppliers\/custom\/.*$" && !req.url ~ "^\/wedding-[\-a-z]*\/deals\/.*$" ) && querystring.remove(req.url) ~ "^\/wedding-(accessories|albums-and-guest-books|beauty-hair-make-up|bridalwear-shop|cakes|cars-and-travel|catering|celebrants|chair-cover|confetti-and-bubbles|decorative-hire|destinations|dress-cleaning-and-boxes|drones|entertainment|favours|fireworks|first-dance-choreography|florist|groom-attire|hen-and-stag-nights|honeymoons|marquee-hire|mobile-bar-services|music-and-djs|nanny|photo-booths|photographers|planner|something-different|speechwriting-services|stationery|sweets-and-treats|toastmaster|videographers|accessory|album|beauty|bridalwear|cake|transport|caterer|celebrants|chaircover|confetti|decorativehire|destination|cleaningandstorage|drone|entertainment|favour|firework|choreography|florist|groomswear|party|honeymoon|marquee|mobilebar|music|nanny|photobooth|photographer|planner|miscelaneous|speechwriter|stationary|confectioner|toastmaster|videographer|suppliers)((\/[\-a-z]+)\/|(\/[\-a-z0-9]+\_[a-z0-9]+)|(\/))$") {
  # set req.http.host = "uat.hitched.co.uk";
  set req.url = querystring.add(req.url, "BetaUser", "SupplierSearchResults");
  set req.backend = F_AWS_Supplier_Catalogue;
}

set req.http.X-Backend = req.backend;

  if (req.request == "GET" && req.url ~ "^\/membertoken$") {
    set req.backend = F_AWS_Supplier_Catalogue;
  }

  if (req.request != "HEAD" && req.request != "GET" && req.request != "FASTLYPURGE") {
    return(pass);
  }

  
    # similarly, don't cache any posts
    if ( req.request == "POST") {
		return (pass);
    }


  return(lookup);
}

sub vcl_fetch {
  
  /* handle 5XX (or any other unwanted status code) */
  if (beresp.status >= 500 && beresp.status < 600) {

    /* deliver stale if the object is available */
    if (stale.exists) {
      return(deliver_stale);
    }

    if (req.restarts < 1 && (req.request == "GET" || req.request == "HEAD")) {
      restart;
    }

    /* else go to vcl_error to deliver a synthetic */
    error 503;
  }

  /* set stale_if_error and stale_while_revalidate (customize these values) */
  set beresp.stale_if_error = 86400s;
  set beresp.stale_while_revalidate = 60s;

  
#FASTLY fetch
  if ((beresp.status == 500 || beresp.status == 503) && req.restarts < 1 && (req.request == "GET" || req.request == "HEAD")) {
    restart;
  }

  if (req.restarts > 0) {
    set beresp.http.Fastly-Restarts = req.restarts;
  }

  set beresp.http.X-Backend = req.http.X-Backend;

  if (beresp.http.Set-Cookie) {
    set req.http.Fastly-Cachetype = "SETCOOKIE";
    return(pass);
  }

  if (beresp.http.Cache-Control ~ "private") {
    set req.http.Fastly-Cachetype = "PRIVATE";
    return(pass);
  }

  if (beresp.status == 500 || beresp.status == 503) {
    set req.http.Fastly-Cachetype = "ERROR";
    set beresp.ttl = 1s;
    set beresp.grace = 5s;
    return(deliver);
  }

  if (beresp.http.Expires || beresp.http.Surrogate-Control ~ "max-age" || beresp.http.Cache-Control ~ "(s-maxage|max-age)") {
    # keep the ttl here
  } else {
    # apply the default ttl
    set beresp.ttl = 3600s;
  }

  return(deliver);
}

sub vcl_hit {
#FASTLY hit

  if (!obj.cacheable) {
    return(pass);
  }
  return(deliver);
}

sub vcl_miss {
#FASTLY miss
  return(fetch);
}

sub vcl_deliver {
  if (resp.status >= 500 && resp.status < 600) {
    /* restart if the stale object is available */
    if (stale.exists) {
      restart;
    }
#FASTLY deliver

  return(deliver);
}
}
sub vcl_error {
#FASTLY error
# Set cookie if not present
/*
if(obj.status == 900) {
     set obj.status = 301;
     set obj.response = "Moved Permanently";
     set obj.http.Location = "https://" + req.http.host + req.url;
     set obj.http.Cache-Control = "no-cache";
    
     synthetic {""};
     return (deliver);
} */
 if (obj.status == 903) {
    #if(randombool(1,2)) {
      set obj.http.Set-Cookie = "BetaUser=SupplierSearchResults; path=/"; 
    #} else {
      #set obj.http.Set-Cookie = "BetaUser=; path=/";
    #}
    
    set obj.status = 301;
    set obj.response = "Moved Permanently";
    set obj.http.Location = "https://" + req.http.host + req.url;
    set obj.http.Cache-Control = "no-cache";
    
    synthetic {""};
    return (deliver);

} else if (obj.status == 902) {

     # Leave
     set obj.status = 301;
     set obj.response = "Moved Permanently";
     set obj.http.Set-Cookie = "BetaUser=; path=/"; 
     set obj.http.Location = "https://" + req.http.host;
     set obj.http.Cache-Control = "no-cache";
     
     synthetic {""};
     return (deliver);
     
} else if (obj.status == 901) {

     # Enroll
     set obj.status = 301;
     set obj.response = "Moved Permanently";
     set obj.http.Set-Cookie = "BetaUser=SupplierSearchResults; path=/";
     set obj.http.Location = "https://" + req.http.host;
     set obj.http.Cache-Control = "no-cache";
     
     synthetic {""};
     return (deliver);
     
} 


/* handle 503s */
  if (obj.status >= 500 && obj.status < 600) {

    /* deliver stale object if it is available */
    if (stale.exists) {
      return(deliver_stale);
    }

    /* otherwise, return a synthetic */

    /* include your HTML response here */
    synthetic {"<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>hitched is currently down while upgrade our servers</title>
    <meta name="description" content="hitched is currently down while upgrade our servers">
    <link type="text/css" rel="stylesheet" href="//fonts.googleapis.com/css?family=Roboto+Slab:700,400|Open+Sans:400italic,600,400" />
    <style type="text/css">
        html { -webkit-font-smoothing:antialiased; -moz-osx-font-smoothing:grayscale; -ms-text-size-adjust:100%; -webkit-text-size-adjust:100%; }
        body { margin:0; -moz-osx-font-smoothing:grayscale; -webkit-font-smoothing:antialiased; }
        h1, p, img { margin:0; padding:0; }
        a, button { color:inherit; transition:.2s; }
        a { text-decoration:none; }
        img { display:block; border:0; height:auto; max-width:100%; }
        * { box-sizing:border-box; -moz-box-sizing:border-box; -webkit-box-sizing:border-box; }
        body { background:#F0EDE8; font-family:'Open Sans', sans-serif; font-weight:400; font-size:16px; color:#4C4C4C; line-height:1.5; }
        h1 { font-family:'Roboto Slab', serif; font-size:26px; font-weight:400; color: #202020; margin:0 0 30px 0; }
        p { margin:0 0 12px 0; }
        .container { background:#fff; width:800px; text-align:center; margin:0 auto; }
        .logo { background:#5f387b; padding:15px; text-align:center; }
        .logo img { display:block; width:150px; height:33px; margin:0 auto; }
        .innerSpacing { padding:30px 20px 50px 20px; }
        @media (max-width:800px) {
            .container { width:100%; }
        }
    </style >
    <!--[if lt IE 9]><script src="http://html5shiv.googlecode.com/svn/trunk/html5.js"></script ><![endif]-->
</head>
<body>
    
    <div class="container">
        <div class="logo"><img src="http://images.hitched.co.uk/logos/hitched.png" border="0" alt="hitched.co.uk" title="hitched.co.uk"></div>
        <div class="innerSpacing">
            <h1>Site Offline</h1>
            <p>We're sorry for any inconvenience.</p>
            <p>We are experiencing technical issues with the site. </p>
            <p>Our best advice is to please try again soon and we'll be back online shortly.</p>
            <p><strong>The hitched team</strong></p>
            <p>Status:"} + obj.status + " " + obj.response + {"</p>
            <p>Backend:"} + req.backend + {"</p>
            <p>CacheType:"} + req.http.Fastly-Cachetype + {"</p>
            <p>URL:"} + req.url + {"</p>
            </div>
    </div>
</body>
</html>"};
    return(deliver);
  }

}

sub vcl_pass {
#FASTLY pass
}

sub vcl_log {
#FASTLY log
}