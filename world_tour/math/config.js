/* The one switchboard for math. Set a skill to false to hide its mode. */
export const ENABLED_SKILLS = {
  dec: true,    // merch table: decimals and money (5.NBT.7)
  md: true,     // ticket sales: multiply and divide (5.NBT.5-6)
  frac: true,   // beat maker: add and subtract fractions (5.NF.1-2)
  xy: true,     // stage designer: coordinate plane (5.G.1-2)
  vol: true,    // pack the truck: volume (5.MD.3-5)
  ops: true,    // tour budget: order of operations (5.OA.1-2)
  flu: true     // soundcheck: facts and powers of ten (5.NBT.1-2)
};

/* grade-level bounds the generators and tests hold to */
export const BOUNDS = {
  moneyCents: 10000,      // money under $100
  maxDenominator: 12,
  coordMax: 10,
  maxVolume: 1000
};
